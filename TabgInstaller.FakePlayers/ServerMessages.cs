using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.FakePlayers
{
    internal static class ServerMessages
    {
        // Keep these payloads byte-for-byte aligned with the decompiled server command readers.
        private static int _nextAiThrownItemIndex = 50000;

        public static void ResetTransientState()
        {
            _nextAiThrownItemIndex = 50000;
        }

        public static void SendLogin(ServerClient server, TABGPlayerServer player)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(player.PlayerName);
            SendToRealClients(server, EventCode.Login, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                writer.Write(player.GroupIndex);
                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);
                writer.Write(player.GearData.Length);
                for (int i = 0; i < player.GearData.Length; i++)
                    writer.Write(player.GearData[i]);
                writer.Write(false);
            }), reliable: true, alsoSendToTeamates: true);
        }

        public static void SendRespawn(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            SendToRealClients(server, EventCode.PlayerRespawn, Write(writer =>
            {
                writer.Write((byte)1);
                writer.Write(player.PlayerIndex);
                writer.Write(player.PlayerIndex);
                writer.Write(player.Health);
                WriteVector3(writer, pos);
                writer.Write(player.PlayerRotation.y);
                writer.Write(byte.MaxValue);
            }), reliable: true, alsoSendToTeamates: true);
        }

        public static void SendVanillaRespawn(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            byte[] packet = RespawnEntityCommand.MakeCommand(server, player, pos, byte.MaxValue);
            SendToRealClients(server, EventCode.PlayerRespawn, packet, reliable: true, alsoSendToTeamates: true);
        }

        public static void SendPlayerUpdate(ServerClient server, TABGPlayerServer player, Vector3 pos)
        {
            if (player != null && player.IsDriving && player.CurrentCar != null)
            {
                SendDrivingPlayerUpdate(server, player);
                return;
            }

            byte[] direction = NetworkOptimizationHelper.OptimizeDirection(player.MovementDirection);
            SendToRealClients(server, EventCode.PlayerUpdate, Write(writer =>
            {
                writer.Write(Time.unscaledTime);
                writer.Write((byte)1);
                writer.Write(player.PlayerIndex);
                writer.Write((byte)PacketContainerFlags.All);
                writer.Write((byte)DrivingState.None);
                WriteVector3(writer, pos);
                writer.Write(player.PlayerRotation.x);
                writer.Write(player.PlayerRotation.y);
                writer.Write(player.IsADS);
                writer.Write(direction);
                writer.Write(player.MovementType);
                writer.Write((byte)0);
            }), reliable: false, alsoSendToTeamates: true);
        }

        public static void SendSeatAccepted(ServerClient server, TABGPlayerServer player, TABGCarServer car, TABGCarServerSeat seat, bool getIn)
        {
            if (server == null || player == null || car == null || seat == null)
                return;

            byte[] carRotation = NetworkOptimizationHelper.OptimizeQuaternion(car.CarRotation);
            SendToRealClients(server, EventCode.SeatAccepted, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                writer.Write(car.CarIndex);
                writer.Write(seat.NetworkIndex);
                writer.Write((byte)(getIn ? SeatAction.GetIn : SeatAction.GetOut));
                if (getIn)
                {
                    WriteVector3(writer, car.CarPosition);
                    writer.Write(carRotation);
                }
            }), reliable: true, alsoSendToTeamates: true);
        }

        public static void SendWeaponChanged(ServerClient server, TABGPlayerServer player)
        {
            SendToRealClients(server, EventCode.WeaponChanged, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                writer.Write((byte)player.Equipment[5]);
                writer.Write(player.Equipment[0]);
                writer.Write(player.Equipment[1]);
                writer.Write(player.Equipment[2]);
                writer.Write(player.Equipment[3]);
                writer.Write(player.Equipment[4]);
                writer.Write((byte)player.Attachments.Length);
                for (int i = 0; i < player.Attachments.Length; i++)
                    writer.Write(player.Attachments[i]);
                writer.Write((short)-1);
            }), reliable: true);
        }

        public static void SendPickupAccepted(ServerClient server, TABGPlayerServer player, NetworkGun loot, byte slot)
        {
            if (server == null || player == null || loot == null)
                return;

            SendToRealClients(server, EventCode.WeaponPickUpAccepted, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                writer.Write(loot.Index);
                writer.Write(loot.UniqueIdentifier);
                writer.Write(loot.Quantity);
                writer.Write(slot);
            }), reliable: true, alsoSendToTeamates: true);
        }

        public static void SendFire(ServerClient server, TABGPlayerServer player, Vector3 target, FiringMode mode)
        {
            Vector3 dir = target - player.PlayerPosition;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();

            byte[] rotBytes = NetworkOptimizationHelper.OptimizeQuaternion(Quaternion.LookRotation(dir));
            SendToRealClients(server, EventCode.PlayerFire, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                writer.Write((byte)(mode | FiringMode.ContainsDirection));
                writer.Write(-1);
                writer.Write(player.PlayerPosition.x);
                writer.Write(player.PlayerPosition.y + 1.3f);
                writer.Write(player.PlayerPosition.z);
                writer.Write(rotBytes);
            }), reliable: true);
        }

        public static void SendFullAutoStop(ServerClient server, TABGPlayerServer player, int bulletsFired)
        {
            if (server == null || player == null)
                return;

            SendToRealClients(server, EventCode.PlayerFire, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                writer.Write((byte)FiringMode.FullAutoStop);
                writer.Write(-1);
                writer.Write(Math.Max(1, bulletsFired));
            }), reliable: true);
        }

        public static void SendGrenadeThrow(ServerClient server, TABGPlayerServer player, int itemIdentifier, int quantity, Vector3 position, Vector3 direction, bool sync)
        {
            if (server == null || player == null)
                return;

            int networkIndex = _nextAiThrownItemIndex++;
            SendToRealClients(server, EventCode.ItemThrown, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                writer.Write(networkIndex);
                writer.Write(itemIdentifier);
                writer.Write(Math.Max(1, quantity));
                WriteVector3(writer, position);
                WriteVector3(writer, direction);
                writer.Write(sync);
            }), reliable: true);
        }

        public static void SendHealthStateChanged(ServerClient server, TABGPlayerServer player, float health)
        {
            SendToRealClients(server, EventCode.PlayerHealthStateChanged, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                writer.Write(health);
            }), reliable: true, alsoSendToTeamates: true);
        }

        public static void SendAirplaneDrop(ServerClient server, TABGPlayerServer player, Vector3 position, Vector3 forward)
        {
            if (server == null || player == null)
                return;

            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            SendToRealClients(server, EventCode.PlayerAirplaneDropped, Write(writer =>
            {
                writer.Write(player.PlayerIndex);
                WriteVector3(writer, position);
                WriteVector3(writer, forward);
            }), reliable: true, alsoSendToTeamates: true);
        }

        public static byte[] MakeDamageCommand(TABGPlayerServer attacker, TABGPlayerServer target, float newHealth)
        {
            Vector3 dir = target.PlayerPosition - attacker.PlayerPosition;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();

            return Write(writer =>
            {
                writer.Write(attacker.PlayerIndex);
                writer.Write(target.PlayerIndex);
                writer.Write(newHealth);
                WriteVector3(writer, dir);
                writer.Write(false);
                writer.Write(false);
            });
        }

        public static void SendDirectDamage(ServerClient server, TABGPlayerServer attacker, TABGPlayerServer target)
        {
            Vector3 dir = target.PlayerPosition - attacker.PlayerPosition;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.forward;
            dir.Normalize();

            byte flags = 0;
            flags = flags.SetBit(2);
            byte[] dirBytes = NetworkOptimizationHelper.OptimizeDirection(dir);
            SendToRealClients(server, EventCode.PlayerDamaged, Write(writer =>
            {
                writer.Write(target.PlayerIndex);
                writer.Write(attacker.PlayerIndex);
                writer.Write(target.Health);
                writer.Write(flags);
                writer.Write(dirBytes);
            }), reliable: true, alsoSendToTeamates: true);
        }

        public static void SendLeave(ServerClient server, byte playerIndex)
        {
            SendToRealClients(
                server,
                EventCode.PlayerLeft,
                new[] { playerIndex, (byte)1 },
                reliable: true,
                alsoSendToTeamates: true);
        }

        public static void SendToRealClients(ServerClient server, EventCode eventCode, byte[] data, bool reliable, bool alsoSendToTeamates = false)
        {
            byte[] recipients = GetRealRecipients(server);
            if (recipients.Length == 0)
                return;

            server.SendMessageToClients(eventCode, data, recipients, reliable, alsoSendToTeamates);
        }

        private static void SendDrivingPlayerUpdate(ServerClient server, TABGPlayerServer player)
        {
            TABGCarServer car = player.CurrentCar;
            byte[] carRotation = NetworkOptimizationHelper.OptimizeQuaternion(car.CarRotation);
            byte[] carInput = NetworkOptimizationHelper.OptimizeDirection(car.CarInput);
            SendToRealClients(server, EventCode.PlayerUpdate, Write(writer =>
            {
                writer.Write(Time.unscaledTime);
                writer.Write((byte)1);
                writer.Write(player.PlayerIndex);
                writer.Write((byte)PacketContainerFlags.All);
                writer.Write((byte)DrivingState.Driving);
                WriteVector3(writer, car.CarPosition);
                writer.Write(carRotation);
                writer.Write(carInput);
                writer.Write(player.PlayerRotation.x);
                writer.Write(player.PlayerRotation.y);
                writer.Write((byte)car.DrivingState);
            }), reliable: false, alsoSendToTeamates: true);
        }

        private static byte[] GetRealRecipients(ServerClient server)
        {
            var room = server != null ? server.GameRoomReference : null;
            if (room == null)
                return Array.Empty<byte>();

            var recipients = new List<byte>();
            for (int i = 0; i < room.Players.Count; i++)
                AddRealRecipient(recipients, room.Players[i]);

            for (int i = 0; i < room.Spectators.Count; i++)
                AddRealRecipient(recipients, room.Spectators[i]);

            return recipients.ToArray();
        }

        private static void AddRealRecipient(List<byte> recipients, TABGPlayerServer player)
        {
            if (player != null && !player.Bot && !recipients.Contains(player.PlayerIndex))
                recipients.Add(player.PlayerIndex);
        }

        private static byte[] Write(Action<BinaryWriter> write)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                write(writer);
                return ms.ToArray();
            }
        }

        private static void WriteVector3(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }
    }
}
