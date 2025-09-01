using Vintagestory.Common.Database;

namespace Haven.Test;

public class FakeGameDbConnection : IGameDbConnection {
  private readonly Dictionary<ulong, byte[]> _chunks = [];
  private readonly Dictionary<ulong, byte[]> _mapChunks = [];
  private readonly Dictionary<ulong, byte[]> _mapRegions = [];
  private byte[] _gameData = null;

  public bool IsReadOnly => false;

  public bool ChunkExists(ulong position) {
    return _chunks.ContainsKey(position);
  }

  public void CreateBackup(string backupFilename) {
    throw new NotImplementedException();
  }

  public void DeleteChunks(IEnumerable<ChunkPos> chunkpositions) {
    foreach (ChunkPos pos in chunkpositions) {
      _chunks.Remove(pos.ToChunkIndex());
    }
  }

  public void DeleteMapChunks(IEnumerable<ChunkPos> chunkpositions) {
    foreach (ChunkPos pos in chunkpositions) {
      _mapChunks.Remove(pos.ToChunkIndex());
    }
  }

  public void DeleteMapRegions(IEnumerable<ChunkPos> chunkpositions) {
    foreach (ChunkPos pos in chunkpositions) {
      _mapRegions.Remove(pos.ToChunkIndex());
    }
  }

  public void Dispose() {
    _chunks.Clear();
    _mapChunks.Clear();
    _mapRegions.Clear();
  }

  public IEnumerable<DbChunk> GetAllChunks() {
    foreach (KeyValuePair<ulong, byte[]> chunk in _chunks) {
      yield return new DbChunk(ChunkPos.FromChunkIndex_saveGamev2(chunk.Key),
                               chunk.Value);
    }
  }

  public IEnumerable<DbChunk> GetAllMapChunks() {
    foreach (KeyValuePair<ulong, byte[]> mapChunk in _mapChunks) {
      yield return new DbChunk(ChunkPos.FromChunkIndex_saveGamev2(mapChunk.Key),
                               mapChunk.Value);
    }
  }

  public IEnumerable<DbChunk> GetAllMapRegions() {
    foreach (KeyValuePair<ulong, byte[]> mapRegion in _mapRegions) {
      yield return new DbChunk(
          ChunkPos.FromChunkIndex_saveGamev2(mapRegion.Key), mapRegion.Value);
    }
  }

  public byte[] GetChunk(ulong coord) {
    _chunks.TryGetValue(coord, out byte[] value);
    return value;
  }

  public byte[] GetGameData() { throw new NotImplementedException(); }

  public byte[] GetMapChunk(ulong coord) {
    _mapChunks.TryGetValue(coord, out byte[] value);
    return value;
  }

  public byte[] GetMapRegion(ulong coord) {
    _mapRegions.TryGetValue(coord, out byte[] value);
    return value;
  }

  public byte[] GetPlayerData(string playeruid) {
    throw new NotImplementedException();
  }

  public bool IntegrityCheck() { throw new NotImplementedException(); }

  public bool MapChunkExists(ulong position) {
    throw new NotImplementedException();
  }

  public bool MapRegionExists(ulong position) {
    throw new NotImplementedException();
  }

  public bool OpenOrCreate(string filename, ref string errorMessage,
                           bool requireWriteAccess, bool corruptionProtection,
                           bool doIntegrityCheck) {
    throw new NotImplementedException();
  }

  public bool QuickCorrectSaveGameVersionTest() {
    throw new NotImplementedException();
  }

  public void SetChunks(IEnumerable<DbChunk> chunks) {
    foreach (DbChunk chunk in chunks) {
      _chunks[chunk.Position.ToChunkIndex()] = chunk.Data;
    }
  }

  public void SetMapChunks(IEnumerable<DbChunk> mapchunks) {
    foreach (DbChunk chunk in mapchunks) {
      _mapChunks[chunk.Position.ToChunkIndex()] = chunk.Data;
    }
  }

  public void SetMapRegions(IEnumerable<DbChunk> mapregions) {
    foreach (DbChunk chunk in mapregions) {
      _mapRegions[chunk.Position.ToChunkIndex()] = chunk.Data;
    }
  }

  public void SetPlayerData(string playeruid, byte[] data) {
    throw new NotImplementedException();
  }

  public void StoreGameData(byte[] data) { _gameData = data; }

  public void UpgradeToWriteAccess() { throw new NotImplementedException(); }

  public void Vacuum() { throw new NotImplementedException(); }
}
