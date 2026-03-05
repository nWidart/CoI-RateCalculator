using Mafi;

namespace RateCalculator;

public struct StatsSummery
{
    public PartialQuantity TotalMaintenance1PerMonth { get; private set; }
    public PartialQuantity TotalMaintenance2PerMonth { get; private set; }
    public PartialQuantity TotalMaintenance3PerMonth { get; private set; }
    public Electricity TotalPowerRequired { get; private set; }
    public int TotalWorkersAssigned { get; private set; }
    public Computing ComputingRequired { get; private set; }

    public void IncrementTotalMaintenance1PerMonth(PartialQuantity maintenancePerMonth) => TotalMaintenance1PerMonth += maintenancePerMonth;
    public void IncrementTotalMaintenance2PerMonth(PartialQuantity maintenancePerMonth) => TotalMaintenance2PerMonth += maintenancePerMonth;
    public void IncrementTotalMaintenance3PerMonth(PartialQuantity maintenancePerMonth) => TotalMaintenance3PerMonth += maintenancePerMonth;

    public void IncrementTotalPowerRequired(Electricity powerRequired) => TotalPowerRequired += powerRequired;
    public void IncrementTotalWorkersAssigned(int workersNeeded) => TotalWorkersAssigned += workersNeeded;
    public void IncrementComputingRequired(Computing computingRequired) => ComputingRequired += computingRequired;

    public override string ToString() =>
        $"Total maintenance costs/month: {this.TotalMaintenance1PerMonth}, power required: {this.TotalPowerRequired}, workers assigned: {this.TotalWorkersAssigned}, computing required: {this.ComputingRequired}";
}