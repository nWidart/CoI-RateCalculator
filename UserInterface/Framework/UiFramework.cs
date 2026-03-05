using Mafi;
using Mafi.Localization;
using Mafi.Unity.Ui.Library;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;

namespace RateCalculator.UserInterface.Framework;

public class UiFramework
{
    public static Panel StartNewPanel(UiComponent[] children)
    {
        var panel = new Panel();
        panel.Body.PaddingTopBottom(2.pt());
        panel.Body.Gap(10.px());
        panel.Body.Add(children);
        return panel;   
    }
    
    public static Column StartNewSection(LocStrFormatted title)
    {
        var component = new Column(2.pt())
        {
            c => c.AlignItemsStretch().PaddingBottom(4.pt()),
            new Title(title),
            new HorizontalDivider().AlignSelfStretch()
        };
        return component;
    }
    
    public static Column StartNewSection(LocStrFormatted title, UiComponent[] children)
    {
        var component = new Column(2.pt())
        {
            c => c.AlignItemsStretch().PaddingBottom(4.pt()),
            new Title(title),
            new HorizontalDivider().AlignSelfStretch(),
            children
        };
        return component;
    }
    
    public static UiComponent StartNewEmptyRow()
    {
        var component = new Row(2.pt());
        component.Add(c => c.PaddingLeft(4.pt()).AlignItemsStretch());
        return component;
    }

    public static UiComponent StartNewRow(UiComponent[] children)
    {
        var component = new Row(2.pt());
        component.Add(c => c.PaddingLeft(4.pt()).AlignItemsStretch());
        component.Add(children);
        return component;
    }

    public static ButtonText NewButtonText(string text)
    {
        return new ButtonText(text.AsLoc()).MinWidth(45.Percent()).MaxWidth(45.Percent());
    }

    public static Label NewLabel(string text)
    {
        return new Label(text.AsLoc());
    }
}