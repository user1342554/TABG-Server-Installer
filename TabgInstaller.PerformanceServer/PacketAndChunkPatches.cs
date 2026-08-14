using System;
using System.Collections.Generic;
using Epic.OnlineServices.AntiCheatCommon;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.PerformanceServer
{
    [HarmonyPatch(typeof(PlayerUpdateCommand), nameof(PlayerUpdateCommand.Run))]
    internal static class PlayerUpdateReaderPatch
    {
        private static bool Prefix(byte[] msgData, ServerClient world)
        {
            if (PerformanceServerPlugin.Instance == null)
                return true;
            if (msgData == null || msgData.Length < 26 || world == null || world.GameRoomReference == null)
                return false;

            var offset = 0;
            var playerIndex = msgData[offset++];
            var position = new Vector3(
                ServerPacketReader.ReadFloat(msgData, ref offset),
                ServerPacketReader.ReadFloat(msgData, ref offset),
                ServerPacketReader.ReadFloat(msgData, ref offset));
            var rotation = new Vector2(
                ServerPacketReader.ReadFloat(msgData, ref offset),
                ServerPacketReader.ReadFloat(msgData, ref offset));
            var ads = msgData[offset++] != 0;
            var movement = ServerPacketReader.ReadDirection(msgData, ref offset);
            var movementFlags = msgData[offset];

            var player = world.GameRoomReference.FindPlayer(playerIndex);
            if (player == null || player.NetworkPlayer == null || player.PlayerObject == null)
                return false;

            player.UpdatePosition(position);
            player.ChangeAimDownSightState(ads);
            player.UpdateRotation(rotation);
            player.UpdateMovementDirection(movement);
            player.UpdateMovementType(movementFlags);
            player.NetworkPlayer.ResetDisconnectTimer();

            var state = AntiCheatCommonPlayerMovementState.None;
            if (movementFlags.IsBitSet(4))
                state = AntiCheatCommonPlayerMovementState.Crouching;
            if (movementFlags.IsBitSet(5))
                state = AntiCheatCommonPlayerMovementState.Prone;
            if (player.IsDriving || player.IsInsideCar)
                state = AntiCheatCommonPlayerMovementState.Mounted;
            if (player.IsSkyDiving)
                state = AntiCheatCommonPlayerMovementState.Flying;

            Easy_AC_Server.Instance.LogPlayerTick(
                player._handle,
                player.Health,
                player.PlayerPosition,
                player.PlayerObject.transform.rotation,
                ads,
                state);
            return false;
        }
    }

    [HarmonyPatch(typeof(ChunkEntryCommand), nameof(ChunkEntryCommand.Run))]
    internal static class ChunkEntryExactPacketPatch
    {
        private static readonly List<TABGPlayerServer> Players = new List<TABGPlayerServer>(64);
        private static readonly List<TABGCarServer> Cars = new List<TABGCarServer>(64);
        private static readonly List<NetworkGun> Loot = new List<NetworkGun>(512);

        private static bool Prefix(ServerClient world, TABGPlayerServer _player)
        {
            if (PerformanceServerPlugin.Instance == null)
                return true;
            if (_player == null || _player.Bot || world == null)
                return false;

            Players.Clear();
            Cars.Clear();
            Loot.Clear();
            var chunks = _player.AreaOfInterest.GetPositiveDiff();
            var serverChunks = ServerChunks.Instance;
            for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var players = serverChunks.GetPlayers(chunks[chunkIndex]);
                if (players != null)
                    for (var index = 0; index < players.Count; index++)
                        if (players[index].HasDropped)
                            Players.Add(players[index]);

                var cars = serverChunks.GetVehicles(chunks[chunkIndex]);
                if (cars != null)
                    Cars.AddRange(cars);
                var loot = serverChunks.GetLoot(chunks[chunkIndex]);
                if (loot != null)
                    Loot.AddRange(loot);
            }

            if (world.GameRoomReference.CurrentGameState == GameState.WaitingForPlayers)
                Cars.Clear();
            if (Players.Count > byte.MaxValue)
                Players.RemoveRange(byte.MaxValue, Players.Count - byte.MaxValue);
            if (Cars.Count > ushort.MaxValue)
                Cars.RemoveRange(ushort.MaxValue, Cars.Count - ushort.MaxValue);
            if (Loot.Count > ushort.MaxValue)
                Loot.RemoveRange(ushort.MaxValue, Loot.Count - ushort.MaxValue);

            var equipmentLength = TABGPlayerServer.EquipmentDataLength;
            var packetLength = 6;
            for (var index = 0; index < Players.Count; index++)
            {
                var player = Players[index];
                packetLength += 18
                                + player.GearData.Length * 4
                                + equipmentLength * 2
                                + player.Attachments.Length * 2;
            }
            packetLength += Loot.Count * 24;
            for (var index = 0; index < Cars.Count; index++)
                packetLength += 20 + QuaternionLength(Cars[index].CarRotation) + Cars[index].NumberOfSeats;

            var packet = new byte[packetLength];
            var offset = 0;
            packet[offset++] = _player.PlayerIndex;
            packet[offset++] = (byte)Players.Count;
            ServerPacketWriter.WriteShort(packet, ref offset, (short)Loot.Count);
            ServerPacketWriter.WriteShort(packet, ref offset, (short)Cars.Count);

            for (var index = 0; index < Players.Count; index++)
            {
                var player = Players[index];
                packet[offset++] = player.PlayerIndex;
                packet[offset++] = (byte)player.PlayerState;
                ServerPacketWriter.WriteInt(packet, ref offset, player.WeaponType);
                ServerPacketWriter.WriteFloat(packet, ref offset, player.Health);
                packet[offset++] = player.IsDowned ? (byte)1 : (byte)0;
                packet[offset++] = player.IsSkyDiving ? (byte)1 : (byte)0;
                ServerPacketWriter.WriteInt(packet, ref offset, player.GearData.Length);
                for (var gearIndex = 0; gearIndex < player.GearData.Length; gearIndex++)
                    ServerPacketWriter.WriteInt(packet, ref offset, player.GearData[gearIndex]);
                packet[offset++] = (byte)equipmentLength;
                for (var equipmentIndex = 0; equipmentIndex < equipmentLength; equipmentIndex++)
                    ServerPacketWriter.WriteShort(packet, ref offset, player.Equipment[equipmentIndex]);
                packet[offset++] = (byte)player.Attachments.Length;
                for (var attachmentIndex = 0; attachmentIndex < player.Attachments.Length; attachmentIndex++)
                    ServerPacketWriter.WriteShort(packet, ref offset, player.Attachments[attachmentIndex]);
            }

            for (var index = 0; index < Loot.Count; index++)
            {
                var item = Loot[index];
                ServerPacketWriter.WriteInt(packet, ref offset, item.Index);
                ServerPacketWriter.WriteInt(packet, ref offset, item.UniqueIdentifier);
                ServerPacketWriter.WriteInt(packet, ref offset, item.Quantity);
                ServerPacketWriter.WriteFloat(packet, ref offset, item.Position.x);
                ServerPacketWriter.WriteFloat(packet, ref offset, item.Position.y);
                ServerPacketWriter.WriteFloat(packet, ref offset, item.Position.z);
            }

            for (var index = 0; index < Cars.Count; index++)
            {
                var car = Cars[index];
                ServerPacketWriter.WriteInt(packet, ref offset, car.CarIndex);
                ServerPacketWriter.WriteFloat(packet, ref offset, car.CarHealth);
                ServerPacketWriter.WriteFloat(packet, ref offset, car.CarPosition.x);
                ServerPacketWriter.WriteFloat(packet, ref offset, car.CarPosition.y);
                ServerPacketWriter.WriteFloat(packet, ref offset, car.CarPosition.z);
                WriteQuaternion(packet, ref offset, car.CarRotation);
                for (var seatIndex = 0; seatIndex < car.NumberOfSeats; seatIndex++)
                    packet[offset++] = car.GetSeat(seatIndex).Occupant?.PlayerIndex ?? byte.MaxValue;
            }

            if (offset != packet.Length)
                throw new InvalidOperationException("Chunk entry packet length mismatch: " + offset + " != " + packet.Length);
            world.SendMessageToClients(EventCode.ChunkEntry, packet, _player.UpdateMessageQueue.Receivers, true);
            return false;
        }

        private static int QuaternionLength(Quaternion value)
        {
            byte ignored;
            float sign;
            var largest = FindLargest(value, out ignored, out sign);
            return Mathf.Approximately(largest, 1f) ? 1 : 7;
        }

        private static void WriteQuaternion(byte[] target, ref int offset, Quaternion value)
        {
            byte largestIndex;
            float sign;
            var largest = FindLargest(value, out largestIndex, out sign);
            if (Mathf.Approximately(largest, 1f))
            {
                target[offset++] = (byte)(largestIndex + 4);
                return;
            }

            target[offset++] = largestIndex;
            switch (largestIndex)
            {
                case 0:
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.y * sign * 10000f));
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.z * sign * 10000f));
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.w * sign * 10000f));
                    break;
                case 1:
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.x * sign * 10000f));
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.z * sign * 10000f));
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.w * sign * 10000f));
                    break;
                case 2:
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.x * sign * 10000f));
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.y * sign * 10000f));
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.w * sign * 10000f));
                    break;
                default:
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.x * sign * 10000f));
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.y * sign * 10000f));
                    ServerPacketWriter.WriteShort(target, ref offset, (short)(value.z * sign * 10000f));
                    break;
            }
        }

        private static float FindLargest(Quaternion value, out byte index, out float sign)
        {
            index = 0;
            var largest = Mathf.Abs(value.x);
            sign = value.x < 0f ? -1f : 1f;
            if (Mathf.Abs(value.y) > largest)
            {
                index = 1;
                largest = Mathf.Abs(value.y);
                sign = value.y < 0f ? -1f : 1f;
            }
            if (Mathf.Abs(value.z) > largest)
            {
                index = 2;
                largest = Mathf.Abs(value.z);
                sign = value.z < 0f ? -1f : 1f;
            }
            if (Mathf.Abs(value.w) > largest)
            {
                index = 3;
                largest = Mathf.Abs(value.w);
                sign = value.w < 0f ? -1f : 1f;
            }
            return largest;
        }
    }

    internal static class ServerPacketReader
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct FloatInt
        {
            [System.Runtime.InteropServices.FieldOffset(0)] internal float Float;
            [System.Runtime.InteropServices.FieldOffset(0)] internal int Int;
        }

        internal static float ReadFloat(byte[] source, ref int offset)
        {
            var converter = new FloatInt { Int = ReadInt(source, ref offset) };
            return converter.Float;
        }

        private static int ReadInt(byte[] source, ref int offset)
        {
            var value = source[offset]
                        | source[offset + 1] << 8
                        | source[offset + 2] << 16
                        | source[offset + 3] << 24;
            offset += 4;
            return value;
        }

        internal static Vector3 ReadDirection(byte[] source, ref int offset)
        {
            var result = new Vector3(
                (source[offset] - 100f) / 100f,
                (source[offset + 1] - 100f) / 100f,
                (source[offset + 2] - 100f) / 100f);
            offset += 3;
            return result;
        }
    }
}
