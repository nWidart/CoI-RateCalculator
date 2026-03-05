using System;
using System.Collections.Generic;
using RateCalculator.Extensions;
using RateCalculator.UserInterface.Framework;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Products;
using Mafi.Core.Syncers;
using Mafi.Localization;
using Mafi.Unity.InputControl;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;

namespace RateCalculator.UserInterface.RateCalculator;

[GlobalDependency(RegistrationMode.AsEverything)]
public class RateCalculatorWindowUi : Window
{
    private StatsSummery _statsSummery;
    private List<ProductProto> _ingredientsDic;
    private List<ProductProto> _productsDic;
    private List<ProductProto> _intermediatesDictionary;
    private Dict<ProductProto, ProductBalance> _balances;
    private Fix32 _globalEfficiency;
    private Dict<ProductProto, Fix32> _efficiencyCache;

    private void SetStats(StatsSummery statsSummery)
    {
        _statsSummery = statsSummery;
    }

    private void SetProducts(List<ProductProto> ingredientsDic, List<ProductProto> productsDic, List<ProductProto> intermediatesDic)
    {
        _ingredientsDic = ingredientsDic;
        _productsDic = productsDic;
        _intermediatesDictionary = intermediatesDic;
    }
    
    private void SetData(Dict<ProductProto, ProductBalance> balances, Dict<ProductProto, List<ProductProto>> dependencies)
    {
        _balances = balances;
        _globalEfficiency = Fix32.One;
        foreach (var (product, balance) in balances)
        {
            if (balance.IsIntermediate && balance.Consumed > Fix32.Zero)
            {
                var ratio = balance.Produced / balance.Consumed;

                if (ratio < _globalEfficiency)
                    _globalEfficiency = ratio;
            }
        }
        
        _efficiencyCache = new Dict<ProductProto, Fix32>();
        foreach (var (product, _) in _balances)
        {
            ComputeEfficiency(product, _balances, dependencies, _efficiencyCache);
        }
    }

    public RateCalculatorWindowUi() : base(new LocStrFormatted("Rate Calculator"), true)
    {
        WindowSize(1000.px(), Px.Auto);
        MakeMovable();
        EnablePinning();

        var tableSection = UiFramework.StartNewSection(new LocStrFormatted("Table"));
        var tableUi = new TableUi.CellRow();
        tableUi.Add(c => c.JustifyItemsCenter().MinHeight(34.px()).Hide());
        tableUi.Add(new Label("No data yet".AsLoc()).FontItalic());
        tableSection.Add(tableUi);

        var statsPanel = UiFramework.StartNewPanel(new[] { GetStatsSection() });
        var tablePanel = UiFramework.StartNewPanel(new[] { tableSection });

        /*
         * ingredients |
         *             |   products      | 
         *             |   intermediates |
         */
        var ingredientsPanel = GetIngredientsPanel();
        var productsPanel = GetProductsPanel();
        var intermediatesPanel = GetIntermediatesPanel();

        Body.Add(statsPanel, ingredientsPanel, productsPanel, intermediatesPanel);
    }
    
    private Panel GetProductsPanel()
    {
        var host = this;
        var productWrapperRow = UiFramework.StartNewEmptyRow();
        var productsSection = UiFramework.StartNewSection("Products".AsLoc());
        productsSection.Add(productWrapperRow);
        
        var productsPanel = UiFramework.StartNewPanel(new[] { productsSection });
        
        this
            .Observe((Func<List<ProductProto>>)(() => host._productsDic))
            .Do(products =>
            {
                productWrapperRow.Clear();
                var row = new Row();
                foreach (var product in products)
                {
                    var icon = new Icon(product)
                        .Size(ProductQuantityUi.ICON_HEIGHT)
                        .MarginRight(10.px());
                    row.Add(icon);

                    var balance = _balances.TryGetValue(product, out var bal) ? bal : new ProductBalance();
                    var theoretical = balance.Produced;

                    var efficiency = Fix32.One;
                    if (_efficiencyCache != null && _efficiencyCache.TryGetValue(product, out var effFromCache))
                        efficiency = effFromCache;

                    var realistic = theoretical * efficiency;

                    var theoreticalStr = theoretical.ToFloat().ToString("0.##");
                    var realisticStr = realistic.ToFloat().ToString("0.##");

                    var producedDisplay = new Display()
                        .Large()
                        .Value($"{theoreticalStr}  →  {realisticStr}".ToDoLoc());

                    if (efficiency < Fix32.One)
                    {
                        producedDisplay.StateWarning();
                        producedDisplay.Tooltip($"Production limited to {(efficiency * 100).ToFloat():0.#}%".AsLoc());
                    }

                    row.Add(producedDisplay);
                }
                productWrapperRow.Add(row);
            });

        return productsPanel;
    }
    
    private Panel GetIngredientsPanel()
    {
        var host = this;
        var wrapperRow = UiFramework.StartNewEmptyRow();
        var productsSection = UiFramework.StartNewSection("Ingredients".AsLoc());
        productsSection.Add(wrapperRow);
        var productsPanel = UiFramework.StartNewPanel(new[] { productsSection });
        
        this
            .Observe((Func<List<ProductProto>>)(() => host._ingredientsDic))
            .Do(products =>
            {
                wrapperRow.Clear();

                var row = new Row();
                foreach (var product in products)
                {
                    var icon = new Icon(product)
                        .Size(ProductQuantityUi.ICON_HEIGHT)
                        .MarginRight(10.px());
                    row.Add(icon);

                    var balance = _balances[product];
                    var consumedDisplay = new Display()
                        .Large()
                        .Value($"{balance.Consumed}".ToDoLoc());

                    row.Add(consumedDisplay);
                }
                wrapperRow.Add(row);
            });
        
        return productsPanel;
    }

    private Panel GetIntermediatesPanel()
    {
        var host = this;
        var wrapperRow = UiFramework.StartNewEmptyRow();
        var productsSection = UiFramework.StartNewSection("Intermediates".AsLoc());
        productsSection.Add(wrapperRow);
        var productsPanel = UiFramework.StartNewPanel(new[] { productsSection });
        
        this
            .Observe((Func<List<ProductProto>>)(() => host._intermediatesDictionary))
            .Do(products =>
            {
                wrapperRow.Clear();

                var row = new Row();

                foreach (var product in products)
                {
                    var icon = new Icon(product)
                        .Large()
                        .MarginRight(10.px());

                    var balance = _balances[product];
                    var display = new Display().Value($"{balance.Produced}".ToDoLoc());
                    display.Large();
                    if (balance.Net < Fix32.Zero)
                    {
                        display.StateDanger();
                        display.Tooltip($"Underproduced by {-balance.Net}".ToDoLoc());
                    }
                    else if (balance.Net > Fix32.Zero)
                    {
                        display.StateWarning();
                        display.Tooltip($"Overproduced by (+{balance.Net})".ToDoLoc());
                    }

                    row.Add(icon);
                    row.Add(display);
                }

                wrapperRow.Add(row);
            });

        return productsPanel;
    }
    
    private Column GetStatsSection()
    {
        var maintenanceLabel = UiFramework.NewLabel("Total maintenance costs/month:");
        UiComponent maintenance1Display = new DisplayWithIcon()
            .IconValue("Assets/Base/Products/Icons/Maintenance1.svg")
            .ObserveValue(() => _statsSummery.TotalMaintenance1PerMonth.ToStringRounded());
        UiComponent maintenance2Display = new DisplayWithIcon()
            .IconValue("Assets/Base/Products/Icons/Maintenance2.svg")
            .ObserveValue(() => _statsSummery.TotalMaintenance2PerMonth.ToStringRounded());
        UiComponent maintenance3Display = new DisplayWithIcon()
            .IconValue("Assets/Base/Products/Icons/Maintenance3.svg")
            .ObserveValue(() => _statsSummery.TotalMaintenance3PerMonth.ToStringRounded());
        var maintenanceRow = UiFramework.StartNewRow(new[] { maintenanceLabel, maintenance1Display, maintenance2Display, maintenance3Display });

        var powerLabel = UiFramework.NewLabel("Total power required: ");
        UiComponent powerDisplay = new DisplayWithIcon()
            .IconValue("Assets/Unity/UserInterface/General/Electricity.svg")
            .Tooltip("Total power required to run selection".AsLoc())
            .ObserveValue(() => _statsSummery.TotalPowerRequired.Format());
        var powerRow = UiFramework.StartNewRow(new[] { powerLabel, powerDisplay });

        var workersLabel = UiFramework.NewLabel("Total workers required: ");
        UiComponent workersDisplay = new DisplayWithIcon()
            .IconValue("Assets/Unity/UserInterface/General/WorkerSmall.svg")
            .ObserveValue(() => _statsSummery.TotalWorkersAssigned);
        var workersRow = UiFramework.StartNewRow(new[] { workersLabel, workersDisplay });

        var computingLabel = UiFramework.NewLabel("Total computing required: ");
        UiComponent computingDisplay = new DisplayWithIcon()
            .IconValue("Assets/Unity/UserInterface/General/Computing128.png")
            .ObserveValue(() => _statsSummery.ComputingRequired.Format());
        var computingRow = UiFramework.StartNewRow(new[] { computingLabel, computingDisplay });

        return UiFramework.StartNewSection(new LocStrFormatted("Statistics"), new[] { maintenanceRow, powerRow, workersRow, computingRow });
    }
    
    private static Fix32 ComputeEfficiency(
        ProductProto product,
        Dict<ProductProto, ProductBalance> balances,
        Dict<ProductProto, List<ProductProto>> deps,
        Dict<ProductProto, Fix32> cache,
        HashSet<ProductProto> visiting = null)
    {
        if (cache.TryGetValue(product, out var cached))
            return cached;

        // Keep track of who we alredy looped over
        visiting ??= new HashSet<ProductProto>();
        if (visiting.Contains(product))
        {
            // if previously visited: return availability
            var cycleAvail = balances.TryGetValue(product, out var b) ? b.Availability : Fix32.One;
            cache[product] = cycleAvail;
            return cycleAvail;
        }
        visiting.Add(product);

        // if we don't have a balance entry treat as unlimited (or: produced=0,consumed=0)
        var balance = balances.TryGetValue(product, out var bal) ? bal : new ProductBalance();

        var efficiency = balance.Availability; // returns 1 when Consumed == 0, else min(1, Produced/Consumed)

        // find lowest efficiency
        if (deps.TryGetValue(product, out var inputs) && inputs.Count > 0)
        {
            foreach (var input in inputs)
            {
                var inputEff = ComputeEfficiency(input, balances, deps, cache, visiting);
                if (inputEff < efficiency)
                    efficiency = inputEff;
                if (efficiency <= Fix32.Zero) break;
            }
        }

        visiting.Remove(product);
        cache[product] = efficiency;
        return efficiency;
    }

    [GlobalDependency(RegistrationMode.AsEverything)]
    public class Controller : WindowController<RateCalculatorWindowUi>
    {
        public Controller(ControllerContext controllerContext) : base(controllerContext)
        {
            controllerContext.UiRoot.AddDependency(this);
            controllerContext.InputManager
                .RegisterGlobalShortcut(_ => ShortcutsMap.Instance.OpenRateCalcWindow, this);
        }

        public void Open() => ActivateSelf();

        public void SetStats(StatsSummery statsSummery)
        {
            Window.SetStats(statsSummery);
        }

        public void SetProducts(List<ProductProto> ingredientsDic, List<ProductProto> productsDic, List<ProductProto> intermediatesDic)
        {
            Window.SetProducts(ingredientsDic, productsDic, intermediatesDic);
        }

        public void SetData(Dict<ProductProto, ProductBalance> balances, Dict<ProductProto, List<ProductProto>> dependencies)
        {
            Window.SetData(balances, dependencies);
        }
    }
}