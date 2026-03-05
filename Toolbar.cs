using System.Collections.Generic;
using System.Linq;
using RateCalculator.UserInterface.RateCalculator;
using Mafi;
using Mafi.Collections;
using Mafi.Collections.ImmutableCollections;
using Mafi.Collections.ReadonlyCollections;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.Machines;
using Mafi.Core.Factory.Transports;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Terrain;
using Mafi.Core.UiState;
using Mafi.Localization;
using Mafi.Unity;
using Mafi.Unity.Entities;
using Mafi.Unity.InputControl;
using Mafi.Unity.InputControl.AreaTool;
using Mafi.Unity.InputControl.Factory;
using Mafi.Unity.Terrain;
using Mafi.Unity.Trains;
using Mafi.Unity.Ui;
using Mafi.Unity.Ui.Controllers;
using Mafi.Unity.Ui.Controllers.LayoutEntityPlacing;
using Mafi.Unity.Ui.Controllers.Tools;
using Mafi.Unity.Ui.Controllers.Trains;
using Mafi.Unity.Ui.Hud;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiStatic;
using Mafi.Unity.UiStatic.Cursors;
using Mafi.Unity.UiToolkit.Component;
using RateCalculator.Extensions;

namespace RateCalculator;

[GlobalDependency(RegistrationMode.AsEverything)]
public class Toolbar : BaseEntityCursorInputController<IStaticEntity>
{
    private readonly EntitiesManager _entitiesManager;
    private readonly ProtosDb _protoDb;
    private readonly StatsSummeryService _statsSummeryService;
    private readonly RateCalculatorWindowUi.Controller _rateCalculatorWindowController;
    private readonly IUnityInputMgr InputManager;
    protected readonly Toolbox Toolbox;

    public Toolbar(
        ToolbarHud toolbar,
        UiContext context,
        CursorPickingManager cursorPickingManager,
        CursorManager cursorManager,
        NewInstanceOf<StaticEntityMassPlacer> entityPlacer,
        AreaSelectionToolFactory areaSelectionToolFactory,
        NewInstanceOf<DockedBuyPricePopup> pricePopup,
        NewInstanceOf<EntityHighlighter> highlighter,
        NewInstanceOf<TransportTrajectoryHighlighter> transportTrajectoryHighlighter,
        NewInstanceOf<TerrainAreaOutlineRenderer> terrainOutlineRenderer,
        EntitiesManager entitiesManager,
        EntitiesCloneConfigHelper configCloneHelper,
        TransportBuildController transportBuildController,
        TrainTrackBuildController trainTrackBuildController,
        TrackDirectionRenderer trackDirectionRenderer,
        HudStateManager hudState,
        ProtosDb protoDb,
        StatsSummeryService statsSummeryService,
        RateCalculatorWindowUi.Controller rateCalculatorWindowController
    ) : base(toolbar,
        context,
        cursorPickingManager,
        cursorManager,
        areaSelectionToolFactory,
        terrainOutlineRenderer,
        entitiesManager,
        highlighter,
        transportTrajectoryHighlighter,
        IdsCore.Technology.CopyTool,
        CursorsStyles.Copy,
        "Assets/Unity/UserInterface/Audio/EntitySelect.prefab",
        (FilterToolbox)new FilterToolboxForCutCopy(hudState,
            nameof(Toolbar)))
    {
        _entitiesManager = entitiesManager;
        _protoDb = protoDb;
        _statsSummeryService = statsSummeryService;
        _rateCalculatorWindowController = rateCalculatorWindowController;

        InputManager = context.InputMgr;
        Toolbox = toolbar.CreateToolbox();
        toolbar.AddToolButton("CalcTool".AsLoc(),
            this,
            "Assets/Unity/UserInterface/Toolbar/Copy.svg",
            1031f,
            manager => ShortcutsMap.Instance.OpenCalc);
        InputManager.RegisterGlobalShortcut(map => ShortcutsMap.Instance.OpenCalc, this);
    }

    public override void Activate()
    {
        Toolbox.Show();
        base.Activate();
    }

    protected override bool Matches(IStaticEntity entity, bool isAreaSelection, bool isLeftClick)
    {
        switch (entity)
        {
            case ILayoutEntity layoutEntity:
                if (layoutEntity.Prototype.CloningDisabled)
                    return false;
                break;
            case TransportPillar _:
                return false;
        }

        return true;
    }

    protected override bool OnFirstActivated(IStaticEntity hoveredEntity, Lyst<IStaticEntity> selectedEntities, Lyst<SubTransport> selectedPartialTransports)
    {
        selectedEntities.Add(hoveredEntity);
        return true;
    }

    protected override void OnEntitiesSelected(IIndexable<IStaticEntity> selectedEntities, IIndexable<SubTransport> selectedPartialTransports,
        ImmutableArray<TileSurfaceCopyPasteData> selectedSurfaces, ImmutableArray<TileSurfaceCopyPasteData> selectedDecals, bool isAreaSelection,
        bool isLeftMouse, RectangleTerrainArea2i? area)
    {
        var statsSummery = _statsSummeryService.GenerateFor(selectedEntities);
        
        var balances = new Dict<ProductProto, ProductBalance>();
        var dependencies = new Dict<ProductProto, List<ProductProto>>();

        foreach (var selectedEntity in selectedEntities)
        {
            if (selectedEntity is not Machine machine) continue;
            if (machine.IsPaused) continue;

            foreach (var recipeProto in machine.RecipesAssigned.AsEnumerable())
            {
                var multiplier = Duration.OneMonth.Ticks / recipeProto.Duration.Ticks.ToFix32();

                var recipeInputs = recipeProto.AllInputs
                    .Select(i => i.Product)
                    .ToList();

                foreach (var recipeInput in recipeProto.AllInputs.AsEnumerable())
                {
                    var inputPerMonth = recipeInput.Quantity.Value * multiplier;
                    var balance = GetOrCreate(balances, recipeInput.Product);
                    balance.Consumed += inputPerMonth;
                }

                foreach (var recipeOutput in recipeProto.AllOutputs.AsEnumerable())
                {
                    var outputPerMonth = recipeOutput.Quantity.Value * multiplier;
                    var balance = GetOrCreate(balances, recipeOutput.Product);
                    balance.Produced += outputPerMonth;

                    var deps = GetOrCreateDeps(dependencies, recipeOutput.Product);
                    foreach (var inp in recipeInputs)
                    {
                        if (!deps.Contains(inp))
                            deps.Add(inp);
                    }
                }

                break; // stop after first recipe for now until I figure out how to handle multiple recipes based on activity and whatnot
            }
        }
        
        var ingredients = new List<ProductProto>();
        var products = new List<ProductProto>();
        var intermediates = new List<ProductProto>();
        
        foreach (var (product, balance) in balances)
        {
            if (balance.IsIngredientOnly)
                ingredients.Add(product);

            else if (balance.IsProductOnly)
                products.Add(product);

            else
                intermediates.Add(product);
        }

        _rateCalculatorWindowController.SetStats(statsSummery);
        _rateCalculatorWindowController.SetProducts(ingredients, products, intermediates);
        _rateCalculatorWindowController.SetData(balances, dependencies);
        
        _rateCalculatorWindowController.Open();
    }
    
    private static ProductBalance GetOrCreate(
        Dict<ProductProto, ProductBalance> dict,
        ProductProto product)
    {
        if (!dict.TryGetValue(product, out var balance))
        {
            balance = new ProductBalance();
            dict.Add(product, balance);
        }
        
        return balance;
    }
    
    private static List<ProductProto> GetOrCreateDeps(
        Dict<ProductProto, List<ProductProto>> depsDict,
        ProductProto product)
    {
        if (!depsDict.TryGetValue(product, out var deps))
        {
            deps = new List<ProductProto>();
            depsDict.Add(product, deps);
        }
        return deps;
    }
}