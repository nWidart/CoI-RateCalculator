using System;
using System.Collections.Generic;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Products;
using Mafi.Core.Syncers;
using Mafi.Localization;
using Mafi.Unity.InputControl;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using RateCalculator.Extensions;
using RateCalculator.UserInterface.Framework;

namespace RateCalculator.UserInterface.RateCalculator;

[GlobalDependency(RegistrationMode.AsEverything)]
public class RateCalculatorWindowUi : Window
{
    private TimeSelectionOption _selectedTimeOption = TimeSelectionOption.Seconds60;
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
        WindowSize(1000.px(), 600.px());
        MakeMovable();
        EnablePinning();

        var optionsRow = new Row();
        optionsRow.AlignItemsCenter();
        optionsRow.PaddingTopBottom(4.pt());
        optionsRow.PaddingRight(10.px());

        // Spacer to push things to the right
        var spacer = new UiComponent();
        spacer.FlexGrow(1);
        optionsRow.Add(spacer);

        var dropdown = new Dropdown<TimeSelectionOption>((opt, idx, isInDropdown) => new Label(opt.ToDisplayText().ToDoLoc()).MinWidth(100.px()));
        dropdown.SetOptions(TimeSelectionOptionExtensions.GetOptions());
        dropdown.OnValueChanged((v, idx) => _selectedTimeOption = v);
        dropdown.Width(150.px());
        optionsRow.Add(dropdown);

        var optionsPanel = UiFramework.StartNewPanel(new[] { optionsRow });
        optionsPanel.AlignSelfStretch();

        var statsRow = GetStatsRow();
        var statsPanel = UiFramework.StartNewPanel(new[] { statsRow });
        statsPanel.AlignSelfStretch();

        var ingredientsPanel = GetProductsPanel(
            "Ingredients",
            host => host._ingredientsDic,
            (container, product) =>
            {
                var icon = new Icon(product)
                    .Size(ProductQuantityUi.ICON_HEIGHT)
                    .MarginRight(10.px());
                container.Add(icon);

                var balance = _balances[product];
                var consumedDisplay = new Display()
                    .Large()
                    .ObserveValue(() => (balance.Consumed * _selectedTimeOption.GetMultiplier()).ToString().ToDoLoc());
                container.Add(consumedDisplay);
                container.MarginRight(15.px());
            },
            isVertical: true);
        ingredientsPanel.Width(300.px());
        ingredientsPanel.AlignSelfStretch();

        var productsPanel = GetProductsPanel(
            "Products",
            host => host._productsDic,
            (container, product) =>
            {
                var icon = new Icon(product)
                    .Size(ProductQuantityUi.ICON_HEIGHT)
                    .MarginRight(10.px());
                container.Add(icon);

                var balance = _balances.TryGetValue(product, out var bal) ? bal : new ProductBalance();
                var producedDisplay = new Display()
                    .Large()
                    .ObserveValue(() => (balance.Produced * _selectedTimeOption.GetMultiplier()).ToString().ToDoLoc());
                container.Add(producedDisplay);
                container.MarginRight(15.px());
            });
        productsPanel.FlexGrow(1);
        productsPanel.AlignSelfStretch();

        var intermediatesPanel = GetProductsPanel(
            "Intermediates",
            host => host._intermediatesDictionary,
            (container, product) =>
            {
                var icon = new Icon(product)
                    .Size(ProductQuantityUi.ICON_HEIGHT)
                    .MarginRight(10.px());
                container.Add(icon);

                var balance = _balances.TryGetValue(product, out var bal) ? bal : new ProductBalance();
                var display = new Display()
                    .Large()
                    .ObserveValue(() => (balance.Produced * _selectedTimeOption.GetMultiplier()).ToString().ToDoLoc());

                if (balance.Net < Fix32.Zero)
                {
                    display.StateDanger();
                    display.Tooltip($"Underproduced by {-(balance.Net * _selectedTimeOption.GetMultiplier())}".ToDoLoc());
                }
                else if (balance.Net > Fix32.Zero)
                {
                    display.StateWarning();
                    display.Tooltip($"Overproduced by (+{balance.Net * _selectedTimeOption.GetMultiplier()})".ToDoLoc());
                }

                container.Add(display);
                container.MarginRight(15.px());
            });
        intermediatesPanel.FlexGrow(1);
        intermediatesPanel.AlignSelfStretch();

        var rightColumn = new Column(10.px());
        rightColumn.Add(productsPanel);
        rightColumn.Add(intermediatesPanel);
        rightColumn.FlexGrow(1);
        rightColumn.AlignItemsStretch();
        rightColumn.AlignSelfStretch();

        var mainLayout = new Row(10.px());
        mainLayout.Add(ingredientsPanel);
        mainLayout.Add(rightColumn);
        mainLayout.FlexGrow(1);
        mainLayout.AlignItemsStretch();

        Body.Gap(10.px());
        Body.Add(optionsPanel);
        Body.Add(statsPanel);
        Body.Add(mainLayout);
    }

    private UiComponent GetStatsRow()
    {
        var maintenance1Display = new DisplayWithIcon()
            .IconValue("Assets/Base/Products/Icons/Maintenance1.svg")
            .Tooltip("Total maintenance 1 costs:".AsLoc())
            .ObserveValue(() => _statsSummery.TotalMaintenance1PerMonth.ToStringRounded());
        var maintenance2Display = new DisplayWithIcon()
            .IconValue("Assets/Base/Products/Icons/Maintenance2.svg")
            .Tooltip("Total maintenance 2 costs:".AsLoc())
            .ObserveValue(() => _statsSummery.TotalMaintenance2PerMonth.ToStringRounded());
        var maintenance3Display = new DisplayWithIcon()
            .IconValue("Assets/Base/Products/Icons/Maintenance3.svg")
            .Tooltip("Total maintenance 3 costs:".AsLoc())
            .ObserveValue(() => _statsSummery.TotalMaintenance3PerMonth.ToStringRounded());

        var powerDisplay = new DisplayWithIcon()
            .IconValue("Assets/Unity/UserInterface/General/Electricity.svg")
            .Tooltip("Total power".AsLoc())
            .ObserveValue(() => _statsSummery.TotalPowerRequired.Format());

        var workersDisplay = new DisplayWithIcon()
            .IconValue("Assets/Unity/UserInterface/General/WorkerSmall.svg")
            .Tooltip("Total workers assigned".AsLoc())
            .ObserveValue(() => _statsSummery.TotalWorkersAssigned);

        var computingDisplay = new DisplayWithIcon()
            .IconValue("Assets/Unity/UserInterface/General/Computing128.png")
            .Tooltip("Total computing required".AsLoc())
            .ObserveValue(() => _statsSummery.ComputingRequired.Format());

        var row = new Row(10.px());
        row.Add(maintenance1Display);
        row.Add(maintenance2Display);
        row.Add(maintenance3Display);
        row.Add(powerDisplay);
        row.Add(workersDisplay);
        row.Add(computingDisplay);
        row.Padding(4.pt());
        return row;
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

    private Panel GetProductsPanel(
        string title,
        Func<RateCalculatorWindowUi, List<ProductProto>> productsSelector,
        Action<Row, ProductProto> addProductUi,
        bool isVertical = false)
    {
        var host = this;

        var section = UiFramework.StartNewSection(title.AsLoc());
        var row = UiFramework.StartNewEmptyRow();
        section.Add(row);

        this
            .Observe((Func<List<ProductProto>>)(() => productsSelector(host)))
            .Do(products =>
            {
                row.Clear();

                var itemsPerRow = isVertical ? 1 : 7;
                var itemContainer = new Column(5.px());
                Row currentRow = null;
                for (int i = 0; i < products.Count; i++)
                {
                    if (i % itemsPerRow == 0)
                    {
                        currentRow = new Row(5.px());
                        itemContainer.Add(currentRow);
                    }
                    addProductUi(currentRow, products[i]);
                }

                row.Add(itemContainer);
            });

        return UiFramework.StartNewPanel(new[] { section });
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