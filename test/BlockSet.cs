using PrefixClassName.MsTest;

using Vintagestory.API.Common;

namespace Haven.Test;

using Real = global::Haven;

[PrefixTestClass]
public class BlockSet {
  [TestMethod]
  public void Deserialize() {
    string json = @"{
      ""includeReplaceable"": 301,
      ""include"": {
        ""game:egg-*"": 1
      },
      ""exclude"": {
        ""rock-*"": 1,
        ""haven:berrybush"": 1,
      },
    }";
    Real.BlockSet blockSet = JsonUtil.ToObject<Real.BlockSet>(json, "haven");
    Assert.Contains(new AssetLocation("game", "egg-*"), blockSet.Include.Keys);
    Assert.Contains(new AssetLocation("game", "rock-*"), blockSet.Exclude.Keys);
    Assert.Contains(new AssetLocation("haven", "berrybush"),
                    blockSet.Exclude.Keys);
    Assert.AreEqual(301, blockSet.IncludeReplaceable);
  }

  [TestMethod]
  public void MergeLists() {
    string json1 = @"{
      ""include"": {
        ""asset1"": 1,
        ""asset2"": 1,
        ""asset3"": 3
      },
      ""exclude"": {
        ""asset1"": 1,
        ""asset2"": 1,
        ""asset3"": 3
      },
    }";
    string json2 = @"{
      ""include"": {
        ""asset2"": 2,
        ""asset3"": 1,
        ""asset4"": 4
      },
      ""exclude"": {
        ""asset2"": 2,
        ""asset3"": 1,
        ""asset4"": 4
      },
    }";
    Real.BlockSet blockSet1 = JsonUtil.ToObject<Real.BlockSet>(json1, "haven");
    Real.BlockSet blockSet2 = JsonUtil.ToObject<Real.BlockSet>(json2, "haven");
    blockSet1.Merge(blockSet2);
    Assert.AreEqual(1, blockSet1.Include["game:asset1"]);
    Assert.AreEqual(2, blockSet1.Include["game:asset2"]);
    Assert.AreEqual(3, blockSet1.Include["game:asset3"]);
    Assert.AreEqual(4, blockSet1.Include["game:asset4"]);

    Assert.AreEqual(1, blockSet1.Exclude["game:asset1"]);
    Assert.AreEqual(2, blockSet1.Exclude["game:asset2"]);
    Assert.AreEqual(3, blockSet1.Exclude["game:asset3"]);
    Assert.AreEqual(4, blockSet1.Exclude["game:asset4"]);
  }

  [TestMethod]
  public void MergeReplaceable() {
    string json1 = @"{
      ""includeReplaceable"": 100
    }";
    string json2 = @"{
      ""includeReplaceable"": 50
    }";
    string json3 = @"{
      ""includeReplaceable"": 200
    }";
    Real.BlockSet blockSet1 = JsonUtil.ToObject<Real.BlockSet>(json1, "haven");
    Real.BlockSet blockSet2 = JsonUtil.ToObject<Real.BlockSet>(json2, "haven");
    Real.BlockSet blockSet3 = JsonUtil.ToObject<Real.BlockSet>(json3, "haven");
    blockSet1.Merge(blockSet2);
    blockSet1.Merge(blockSet3);
    Assert.AreEqual(50, blockSet1.IncludeReplaceable);
  }

  [TestMethod]
  public void Resolve() {
    string json = @"{
      ""includeReplaceable"": 6000,
      ""include"": {
        ""rock-andesite"": 2,
        ""rock-granite"": 2
      },
      ""exclude"": {
        ""rock-andesite"": 2,
        ""rock-granite"": 1
      },
    }";
    Real.BlockSet blockSet = JsonUtil.ToObject<Real.BlockSet>(json, "haven");
    Real.MatchResolver resolver =
        new(Framework.Api.World, Framework.Api.Logger);
    HashSet<int> blocks = blockSet.Resolve(resolver);
    Block granite =
        Framework.Server.World.GetBlock(new AssetLocation("game:rock-granite"));
    Block andesite = Framework.Server.World.GetBlock(
        new AssetLocation("game:rock-andesite"));
    Block chalk =
        Framework.Server.World.GetBlock(new AssetLocation("game:rock-chalk"));
    Block grass = Framework.Server.World.GetBlock(
        new AssetLocation("game:tallgrass-short-free"));
    Assert.DoesNotContain(andesite.Id, blocks);
    Assert.Contains(granite.Id, blocks);
    Assert.DoesNotContain(chalk.Id, blocks);
    Assert.Contains(grass.Id, blocks);
  }
}
