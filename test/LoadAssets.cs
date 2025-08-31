using Microsoft.VisualStudio.TestTools.UnitTesting;

using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Server;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class LoadAssets {
  // This property is set by the test framework:
  // https://learn.microsoft.com/en-us/visualstudio/test/how-to-create-a-data-driven-unit-test?view=vs-2022#add-a-testcontext-to-the-test-class
  public TestContext TestContext { get; set; } = null!;
  public static ServerMain Server = null;

  [AssemblyInitialize()]
  public static void AssemblyInitialize(TestContext context) {
    Dictionary<AssetCategory, HashSet<string>> allow =
        new() { [AssetCategory.itemtypes] = new() { "fruit.json" },
                [AssetCategory.blocktypes] = new() { "rock.json" },
                [AssetCategory.recipes] = new() {} };
    Server = ServerApiWithAssets.Create(allow);
  }

  [AssemblyCleanup()]
  public static void AssemblyCleanup() { Server?.Dispose(); }

  public static Item GetItem(string domain, string code) {
    return Server.World.GetItem(new AssetLocation(domain, code));
  }

  public static Block GetBlock(string domain, string code) {
    return Server.World.GetBlock(new AssetLocation(domain, code));
  }

  [TestMethod]
  public void CranberryItemLoaded() {
    Item cranberry =
        Server.World.GetItem(new AssetLocation("game:fruit-cranberry"));
    Assert.IsNotNull(cranberry);
    Assert.AreEqual(EnumFoodCategory.Fruit,
                    cranberry.NutritionProps.FoodCategory);
  }

  [TestMethod]
  public void GraniteBlockLoaded() {
    Block granite =
        Server.World.GetBlock(new AssetLocation("game:rock-granite"));
    Assert.IsNotNull(granite);
    Assert.IsGreaterThanOrEqualTo(5, granite.Resistance);
  }
}
