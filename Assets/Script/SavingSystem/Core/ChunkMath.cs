using System;
using UnityEngine;

namespace SavingSystem.Core
{
    /// <summary>
    /// Represents a 3D chunk identifier using integer coordinates.
    /// Chunks are indexed relative to the world origin using floor(position / chunkSize).
    /// </summary>
    [Serializable]
    public struct ChunkID : IEquatable<ChunkID>
    {
        public int X;
        public int Y;
        public int Z;

        public ChunkID(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Computes the ChunkID for a given world position and chunk size.
        /// Uses pure math (floor division), no Unity physics.
        /// </summary>
        public static ChunkID FromWorldPosition(Vector3 worldPosition, Vector3 chunkSize)
        {
            return new ChunkID(
                Mathf.FloorToInt(worldPosition.x / chunkSize.x),
                Mathf.FloorToInt(worldPosition.y / chunkSize.y),
                Mathf.FloorToInt(worldPosition.z / chunkSize.z)
            );
        }

        /// <summary>
        /// Gets the world-space center position of this chunk.
        /// </summary>
        public Vector3 GetWorldCenter(Vector3 chunkSize)
        {
            return new Vector3(
                (X + 0.5f) * chunkSize.x,
                (Y + 0.5f) * chunkSize.y,
                (Z + 0.5f) * chunkSize.z
            );
        }

        /// <summary>
        /// Gets the world-space minimum corner of this chunk.
        /// </summary>
        public Vector3 GetWorldMin(Vector3 chunkSize)
        {
            return new Vector3(
                X * chunkSize.x,
                Y * chunkSize.y,
                Z * chunkSize.z
            );
        }

        /// <summary>
        /// Gets the world-space maximum corner of this chunk.
        /// </summary>
        public Vector3 GetWorldMax(Vector3 chunkSize)
        {
            return new Vector3(
                (X + 1) * chunkSize.x,
                (Y + 1) * chunkSize.y,
                (Z + 1) * chunkSize.z
            );
        }

        /// <summary>
        /// Calculates the Manhattan distance between two chunks.
        /// </summary>
        public int ManhattanDistance(ChunkID other)
        {
            return Mathf.Abs(X - other.X) + Mathf.Abs(Y - other.Y) + Mathf.Abs(Z - other.Z);
        }

        /// <summary>
        /// Calculates the Chebyshev distance (max axis distance) between two chunks.
        /// Useful for determining if a chunk is within a cubic range.
        /// </summary>
        public int ChebyshevDistance(ChunkID other)
        {
            return Mathf.Max(
                Mathf.Abs(X - other.X),
                Mathf.Abs(Y - other.Y),
                Mathf.Abs(Z - other.Z)
            );
        }

        /// <summary>
        /// Checks if this chunk is within the specified range of another chunk.
        /// </summary>
        public bool IsWithinRange(ChunkID center, Vector3Int halfExtents)
        {
            return Mathf.Abs(X - center.X) <= halfExtents.x &&
                   Mathf.Abs(Y - center.Y) <= halfExtents.y &&
                   Mathf.Abs(Z - center.Z) <= halfExtents.z;
        }

        public bool Equals(ChunkID other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkID other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Use prime multiplication for better hash distribution
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X;
                hash = hash * 31 + Y;
                hash = hash * 31 + Z;
                return hash;
            }
        }

        public static bool operator ==(ChunkID left, ChunkID right) => left.Equals(right);
        public static bool operator !=(ChunkID left, ChunkID right) => !left.Equals(right);

        public override string ToString() => $"Chunk({X}, {Y}, {Z})";
    }

    /// <summary>
    /// Represents a unique entity identifier.
    /// Can be generated or assigned from external sources (database, network, etc.)
    /// </summary>
    [Serializable]
    public struct EntityID : IEquatable<EntityID>
    {
        [SerializeField] private ulong _id;

        public ulong Value => _id;

        public EntityID(ulong id)
        {
            _id = id;
        }

        /// <summary>
        /// Creates an EntityID from a GUID (useful for editor-assigned IDs).
        /// </summary>
        public static EntityID FromGuid(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            return new EntityID(BitConverter.ToUInt64(bytes, 0) ^ BitConverter.ToUInt64(bytes, 8));
        }

        /// <summary>
        /// Creates a new unique EntityID using a combination of timestamp and random.
        /// </summary>
        public static EntityID GenerateUnique()
        {
            // Combine timestamp with random for uniqueness
            long timestamp = DateTime.UtcNow.Ticks;
            int random = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            ulong id = ((ulong)timestamp << 32) | (uint)random;
            return new EntityID(id);
        }

        public bool IsValid => _id != 0;

        public static EntityID Invalid => new EntityID(0);

        public bool Equals(EntityID other) => _id == other._id;

        public override bool Equals(object obj) => obj is EntityID other && Equals(other);

        public override int GetHashCode() => _id.GetHashCode();

        public static bool operator ==(EntityID left, EntityID right) => left.Equals(right);
        public static bool operator !=(EntityID left, EntityID right) => !left.Equals(right);

        public override string ToString() => $"Entity({_id:X16})";
    }

    /// <summary>
    /// Static utility class for chunk-related mathematical operations.
    /// All operations are pure math, no Unity physics involved.
    /// </summary>
    public static class ChunkMathUtility
    {
        /// <summary>
        /// Enumerates all chunk IDs within a rectangular bounds around a center chunk.
        /// </summary>
        public static void GetChunksInRange(
            ChunkID center,
            Vector3Int halfExtents,
            Action<ChunkID> onChunk)
        {
            for (int x = center.X - halfExtents.x; x <= center.X + halfExtents.x; x++)
            {
                for (int y = center.Y - halfExtents.y; y <= center.Y + halfExtents.y; y++)
                {
                    for (int z = center.Z - halfExtents.z; z <= center.Z + halfExtents.z; z++)
                    {
                        onChunk(new ChunkID(x, y, z));
                    }
                }
            }
        }

        /// <summary>
        /// Gets the total number of chunks in a range.
        /// </summary>
        public static int GetChunkCountInRange(Vector3Int halfExtents)
        {
            return (halfExtents.x * 2 + 1) * (halfExtents.y * 2 + 1) * (halfExtents.z * 2 + 1);
        }

        /// <summary>
        /// Converts a world position to chunk-local coordinates.
        /// </summary>
        public static Vector3 WorldToChunkLocal(Vector3 worldPosition, ChunkID chunk, Vector3 chunkSize)
        {
            Vector3 chunkMin = chunk.GetWorldMin(chunkSize);
            return worldPosition - chunkMin;
        }

        /// <summary>
        /// Converts chunk-local coordinates to world position.
        /// </summary>
        public static Vector3 ChunkLocalToWorld(Vector3 localPosition, ChunkID chunk, Vector3 chunkSize)
        {
            Vector3 chunkMin = chunk.GetWorldMin(chunkSize);
            return localPosition + chunkMin;
        }

        /// <summary>
        /// Checks if a world position is inside a specific chunk.
        /// </summary>
        public static bool IsPositionInChunk(Vector3 worldPosition, ChunkID chunk, Vector3 chunkSize)
        {
            ChunkID posChunk = ChunkID.FromWorldPosition(worldPosition, chunkSize);
            return posChunk == chunk;
        }
    }
}
