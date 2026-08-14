using System;
using System.Collections.Generic;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.NetworkQueue;
using UnityEngine;

namespace TabgInstaller.PerformanceServer
{
    [HarmonyPatch(typeof(NetworkQueue), nameof(NetworkQueue.SendMessages))]
    internal static class NetworkQueueSendPatch
    {
        private static readonly List<NetworkMessage> PlayerMessages = new List<NetworkMessage>(32);
        private static readonly List<NetworkMessage> CarMessages = new List<NetworkMessage>(16);

        private static bool Prefix(
            NetworkQueue __instance,
            ServerClient world,
            Queue<NetworkMessage> ___m_messageQueue,
            Queue<NetworkMessage> ___m_carMessageQueue,
            EventCode ___m_eventType,
            bool ___m_sendReliable,
            ref int ___m_queueSize,
            ref int ___m_carQueueSize,
            ref byte[] __result)
        {
            if (PerformanceServerPlugin.Instance == null)
                return true;
            if (___m_messageQueue.Count == 0 && ___m_carMessageQueue.Count == 0)
            {
                __result = Array.Empty<byte>();
                return false;
            }
            if (___m_messageQueue.Count >= 256 || ___m_carMessageQueue.Count >= 256)
            {
                LandLog.LogError("NetworkQueue overflow; dropping stale queued updates.");
                Clear(___m_messageQueue, ___m_carMessageQueue, ref ___m_queueSize, ref ___m_carQueueSize);
                __result = Array.Empty<byte>();
                return false;
            }

            var maximumSize = PerformanceServerPlugin.Instance.MaximumQueuePacketSize;
            byte[] lastPacket = null;
            while (___m_messageQueue.Count > 0 || ___m_carMessageQueue.Count > 0)
            {
                PlayerMessages.Clear();
                CarMessages.Clear();
                var size = 6; // timestamp + player count + car count

                while (___m_messageQueue.Count > 0 && PlayerMessages.Count < byte.MaxValue)
                {
                    var next = ___m_messageQueue.Peek();
                    if (size + next.Data.Length > maximumSize && PlayerMessages.Count + CarMessages.Count > 0)
                        break;
                    ___m_messageQueue.Dequeue();
                    PlayerMessages.Add(next);
                    size += next.Data.Length;
                }
                while (___m_carMessageQueue.Count > 0 && CarMessages.Count < byte.MaxValue)
                {
                    var next = ___m_carMessageQueue.Peek();
                    if (size + next.Data.Length > maximumSize && PlayerMessages.Count + CarMessages.Count > 0)
                        break;
                    ___m_carMessageQueue.Dequeue();
                    CarMessages.Add(next);
                    size += next.Data.Length;
                }

                // If player messages filled the packet exactly, cars remain for
                // the next packet. If neither loop progressed, force one message.
                if (PlayerMessages.Count == 0 && CarMessages.Count == 0)
                {
                    if (___m_messageQueue.Count > 0)
                    {
                        var next = ___m_messageQueue.Dequeue();
                        PlayerMessages.Add(next);
                        size += next.Data.Length;
                    }
                    else
                    {
                        var next = ___m_carMessageQueue.Dequeue();
                        CarMessages.Add(next);
                        size += next.Data.Length;
                    }
                }

                lastPacket = new byte[size];
                var offset = 0;
                ServerPacketWriter.WriteFloat(lastPacket, ref offset, Time.unscaledTime);
                lastPacket[offset++] = (byte)PlayerMessages.Count;
                for (var index = 0; index < PlayerMessages.Count; index++)
                {
                    var data = PlayerMessages[index].Data;
                    Buffer.BlockCopy(data, 0, lastPacket, offset, data.Length);
                    offset += data.Length;
                }
                lastPacket[offset++] = (byte)CarMessages.Count;
                for (var index = 0; index < CarMessages.Count; index++)
                {
                    var data = CarMessages[index].Data;
                    Buffer.BlockCopy(data, 0, lastPacket, offset, data.Length);
                    offset += data.Length;
                }
                world.SendMessageToClients(___m_eventType, lastPacket, __instance.Receivers, ___m_sendReliable);
            }

            Clear(___m_messageQueue, ___m_carMessageQueue, ref ___m_queueSize, ref ___m_carQueueSize);
            __result = lastPacket ?? Array.Empty<byte>();
            return false;
        }

        private static void Clear(
            Queue<NetworkMessage> messages,
            Queue<NetworkMessage> cars,
            ref int messageSize,
            ref int carSize)
        {
            messages.Clear();
            cars.Clear();
            messageSize = 0;
            carSize = 0;
        }
    }

    [HarmonyPatch(typeof(EntityUpdatesCommand), nameof(EntityUpdatesCommand.Run))]
    internal static class EntityDeltaSnapshotPatch
    {
        private const PacketContainerFlags EveryDirtyField =
            PacketContainerFlags.PlayerPosition
            | PacketContainerFlags.PlayerRotation
            | PacketContainerFlags.PlayerDirection
            | PacketContainerFlags.CarPosition
            | PacketContainerFlags.CarRotation
            | PacketContainerFlags.CarInput;

        private static int _runIndex;
        private static bool _reverseSendOrder;

        private static bool Prefix(byte[] msgData, ServerClient world, int messageCullIndex)
        {
            var plugin = PerformanceServerPlugin.Instance;
            if (plugin == null)
                return true;
            if (world == null || world.GameRoomReference == null)
                return false;

            _runIndex++;
            var room = world.GameRoomReference;
            var players = room.Players;
            var sendFarTier = messageCullIndex == 1 || messageCullIndex == 3;
            var nearDistance = ServerChunks.CHUNK_SIZE / 6f;
            var nearDistanceSquared = nearDistance * nearDistance;

            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index];
                if (room.CurrentGameState == GameState.Started && (!player.HasDropped || player.IsDead))
                    continue;

                var drivingState = player.IsDriving
                    ? DrivingState.Driving
                    : player.IsInsideCar ? DrivingState.InsideCar : DrivingState.None;
                var flags = GetSnapshot(player, plugin);
                var packet = BuildPlayerPacket(player, drivingState, flags);
                player.SnapShotTaken(flags == PacketContainerFlags.All ? EveryDirtyField : flags);

                var watchers = ServerChunks.Instance.GetWatchers(player.ChunkData);
                if (watchers == null)
                    continue;
                for (var watcherIndex = 0; watcherIndex < watchers.Count; watcherIndex++)
                {
                    var watcher = watchers[watcherIndex];
                    if (watcher.PlayerIndex == player.PlayerIndex || watcher.Bot)
                        continue;
                    var delta = player.PlayerPosition - watcher.PlayerPosition;
                    if (delta.sqrMagnitude < nearDistanceSquared || sendFarTier)
                        watcher.UpdateMessageQueue.EnqueueMessage(packet);
                }
            }

            var cars = room.Cars;
            for (var index = 0; index < cars.Count; index++)
            {
                var car = cars[index];
                if (!car.HasTemporaryOwner || !car.TemporaryChanged)
                    continue;
                var packet = BuildCarPacket(car);
                var watchers = ServerChunks.Instance.GetWatchers(car.ChunkData);
                if (watchers == null)
                    continue;
                for (var watcherIndex = 0; watcherIndex < watchers.Count; watcherIndex++)
                {
                    var watcher = watchers[watcherIndex];
                    if (watcher.PlayerIndex == car.TemporaryOwner)
                        continue;
                    var delta = car.CarPosition - watcher.PlayerPosition;
                    if (delta.sqrMagnitude < nearDistanceSquared || sendFarTier)
                        watcher.UpdateMessageQueue.EnqueCarMessage(packet);
                }
            }

            _reverseSendOrder = !_reverseSendOrder;
            if (_reverseSendOrder)
            {
                for (var index = players.Count - 1; index >= 0; index--)
                    if (!players[index].Bot)
                        players[index].SendMessages(world);
            }
            else
            {
                for (var index = 0; index < players.Count; index++)
                    if (!players[index].Bot)
                        players[index].SendMessages(world);
            }

            var spectators = room.Spectators;
            for (var index = 0; index < spectators.Count; index++)
                if (!spectators[index].Bot)
                    spectators[index].SendMessages(world);
            return false;
        }

        private static PacketContainerFlags GetSnapshot(TABGPlayerServer player, PerformanceServerPlugin plugin)
        {
            if (!plugin.DeltaSnapshots || _runIndex % plugin.KeyframeInterval == 0)
                return PacketContainerFlags.All;
            return player.GetPacketSnapShot();
        }

        private static byte[] BuildPlayerPacket(TABGPlayerServer player, DrivingState state, PacketContainerFlags flags)
        {
            var hasPosition = Has(flags, PacketContainerFlags.PlayerPosition);
            var hasRotation = Has(flags, PacketContainerFlags.PlayerRotation);
            var hasDirection = Has(flags, PacketContainerFlags.PlayerDirection);
            var hasCarPosition = Has(flags, PacketContainerFlags.CarPosition);
            var hasCarRotation = Has(flags, PacketContainerFlags.CarRotation);
            var hasCarInput = Has(flags, PacketContainerFlags.CarInput);
            var driving = (state & DrivingState.Driving) == DrivingState.Driving;
            var packetLength = 3;
            if (driving)
            {
                if (hasCarPosition) packetLength += 12;
                if (hasCarRotation) packetLength += QuaternionLength(player.CurrentCar.CarRotation);
                if (hasCarInput) packetLength += 3;
                if (hasRotation) packetLength += 8;
                packetLength += 1;
            }
            else
            {
                if (hasPosition) packetLength += 12;
                if (hasRotation) packetLength += 8;
                packetLength += 2; // ADS + movement type
                if (hasDirection) packetLength += 3;
            }

            var packet = new byte[packetLength];
            var offset = 0;
            packet[offset++] = player.PlayerIndex;
            packet[offset++] = (byte)flags;
            packet[offset++] = (byte)state;
            if (driving)
            {
                if (hasCarPosition) WriteVector3(packet, ref offset, player.CurrentCar.CarPosition);
                if (hasCarRotation) WriteQuaternion(packet, ref offset, player.CurrentCar.CarRotation);
                if (hasCarInput) WriteDirection(packet, ref offset, player.CurrentCar.CarInput);
                if (hasRotation) WriteVector2(packet, ref offset, player.PlayerRotation);
                packet[offset++] = (byte)player.CurrentCar.DrivingState;
            }
            else
            {
                if (hasPosition) WriteVector3(packet, ref offset, player.PlayerPosition);
                if (hasRotation) WriteVector2(packet, ref offset, player.PlayerRotation);
                packet[offset++] = player.IsADS ? (byte)1 : (byte)0;
                if (hasDirection) WriteDirection(packet, ref offset, player.MovementDirection);
                packet[offset++] = player.MovementType;
            }
            return packet;
        }

        private static byte[] BuildCarPacket(TABGCarServer car)
        {
            var packet = new byte[16 + QuaternionLength(car.CarRotation)];
            var offset = 0;
            ServerPacketWriter.WriteInt(packet, ref offset, car.CarIndex);
            WriteVector3(packet, ref offset, car.CarPosition);
            WriteQuaternion(packet, ref offset, car.CarRotation);
            return packet;
        }

        private static bool Has(PacketContainerFlags flags, PacketContainerFlags value)
        {
            return flags == PacketContainerFlags.All || (flags & value) != PacketContainerFlags.Nothing;
        }

        private static void WriteVector2(byte[] target, ref int offset, Vector2 value)
        {
            ServerPacketWriter.WriteFloat(target, ref offset, value.x);
            ServerPacketWriter.WriteFloat(target, ref offset, value.y);
        }

        private static void WriteVector3(byte[] target, ref int offset, Vector3 value)
        {
            ServerPacketWriter.WriteFloat(target, ref offset, value.x);
            ServerPacketWriter.WriteFloat(target, ref offset, value.y);
            ServerPacketWriter.WriteFloat(target, ref offset, value.z);
        }

        private static void WriteDirection(byte[] target, ref int offset, Vector3 value)
        {
            target[offset++] = (byte)(value.x * 100f + 100f);
            target[offset++] = (byte)(value.y * 100f + 100f);
            target[offset++] = (byte)(value.z * 100f + 100f);
        }

        private static int QuaternionLength(Quaternion value)
        {
            byte index;
            float sign;
            return Mathf.Approximately(FindLargest(value, out index, out sign), 1f) ? 1 : 7;
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
            if (largestIndex != 0) ServerPacketWriter.WriteShort(target, ref offset, (short)(value.x * sign * 10000f));
            if (largestIndex != 1) ServerPacketWriter.WriteShort(target, ref offset, (short)(value.y * sign * 10000f));
            if (largestIndex != 2) ServerPacketWriter.WriteShort(target, ref offset, (short)(value.z * sign * 10000f));
            if (largestIndex != 3) ServerPacketWriter.WriteShort(target, ref offset, (short)(value.w * sign * 10000f));
        }

        private static float FindLargest(Quaternion value, out byte index, out float sign)
        {
            index = 0;
            var largest = Mathf.Abs(value.x);
            sign = value.x < 0f ? -1f : 1f;
            if (Mathf.Abs(value.y) > largest) { index = 1; largest = Mathf.Abs(value.y); sign = value.y < 0f ? -1f : 1f; }
            if (Mathf.Abs(value.z) > largest) { index = 2; largest = Mathf.Abs(value.z); sign = value.z < 0f ? -1f : 1f; }
            if (Mathf.Abs(value.w) > largest) { index = 3; largest = Mathf.Abs(value.w); sign = value.w < 0f ? -1f : 1f; }
            return largest;
        }
    }

    [HarmonyPatch(typeof(ServerChunks), nameof(ServerChunks.GetWatchers))]
    internal static class ServerChunkWatcherListPatch
    {
        private static bool Prefix(ServerChunks __instance, ChunkDataServer chunkPos, ref List<TABGPlayerServer> __result)
        {
            if (PerformanceServerPlugin.Instance == null)
                return true;
            ServerChunk chunk;
            if (!__instance.GridTiles.TryGetValue(chunkPos, out chunk))
            {
                __result = null;
                return false;
            }
            __result = chunk.GetWatchers();
            return false;
        }
    }

    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.FindPlayer), typeof(byte))]
    internal static class GameRoomDictionaryLookupPatch
    {
        private static bool Prefix(byte index, Dictionary<byte, TABGPlayerServer> ___m_playerIndexDictionary, ref TABGPlayerServer __result)
        {
            if (PerformanceServerPlugin.Instance == null)
                return true;
            ___m_playerIndexDictionary.TryGetValue(index, out __result);
            return false;
        }
    }

    [HarmonyPatch(typeof(ServerClient), "OnGUI")]
    internal static class ProductionGuiPatch
    {
        private static bool Prefix()
        {
            var plugin = PerformanceServerPlugin.Instance;
            return plugin == null || !plugin.DisableProductionGui || Application.isEditor;
        }
    }

    internal static class ServerPacketWriter
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct FloatInt
        {
            [System.Runtime.InteropServices.FieldOffset(0)] internal float Float;
            [System.Runtime.InteropServices.FieldOffset(0)] internal int Int;
        }

        internal static void WriteFloat(byte[] target, ref int offset, float value)
        {
            var converter = new FloatInt { Float = value };
            WriteInt(target, ref offset, converter.Int);
        }

        internal static void WriteInt(byte[] target, ref int offset, int value)
        {
            target[offset++] = (byte)value;
            target[offset++] = (byte)(value >> 8);
            target[offset++] = (byte)(value >> 16);
            target[offset++] = (byte)(value >> 24);
        }

        internal static void WriteShort(byte[] target, ref int offset, short value)
        {
            target[offset++] = (byte)value;
            target[offset++] = (byte)(value >> 8);
        }
    }
}
