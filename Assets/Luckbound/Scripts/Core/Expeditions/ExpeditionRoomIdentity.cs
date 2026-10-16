using System;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Expeditions
{
    /// <summary>Derives a persistent room identity from one expedition and one route node.</summary>
    public static class ExpeditionRoomIdentity
    {
        private const string IdentityDomain = "Luckbound.ExpeditionRoom.v1\n";

        public static RoomInstanceId ForNode(RunId runId, string nodeId)
        {
            if (!runId.IsValid) throw new ArgumentException("A room identity requires a valid expedition RunId.", nameof(runId));
            if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("A room identity requires a route node ID.", nameof(nodeId));

            byte[] name = Encoding.UTF8.GetBytes(IdentityDomain + runId + "\n" + nodeId);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create()) digest = sha256.ComputeHash(name);

            var guidBytes = new byte[16];
            Buffer.BlockCopy(digest, 0, guidBytes, 0, guidBytes.Length);
            Guid value = new Guid(guidBytes);
            if (value == Guid.Empty)
            {
                guidBytes[0] = 1;
                value = new Guid(guidBytes);
            }
            return new RoomInstanceId(value);
        }
    }
}
