using Mafi;
using Mafi.Collections.ReadonlyCollections;
using Mafi.Core.Entities.Static;
using Mafi.Core.Factory.ComputingPower;
using Mafi.Core.Factory.ElectricPower;
using Mafi.Core.Maintenance;
using Mafi.Core.Population;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;

namespace RateCalculator;

[GlobalDependency(RegistrationMode.AsSelf)]
public class StatsSummeryService
{
    private readonly ProtosDb _protoDb;

    public StatsSummeryService(ProtosDb protoDb)
    {
        _protoDb = protoDb;
    }

    public StatsSummery GenerateFor(IIndexable<IStaticEntity> selectedEntities)
    {
        var maintT1 = _protoDb.Get<ProductProto>(Mafi.Base.Ids.Products.MaintenanceT1).Value;
        var maintT2 = _protoDb.Get<ProductProto>(Mafi.Base.Ids.Products.MaintenanceT2).Value;
        var maintT3 = _protoDb.Get<ProductProto>(Mafi.Base.Ids.Products.MaintenanceT3).Value;

        var statsSummery = new StatsSummery();
        foreach (var selectedEntity in selectedEntities)
        {
            if (selectedEntity.IsPaused) continue;
            if (selectedEntity is IMaintainedEntity maintainedEntity)
            {
                var maintenancePerMonth = maintainedEntity.Maintenance.Costs.MaintenancePerMonth;

                if (maintainedEntity.Maintenance.Costs.Product.Id.Equals(maintT1.Id))
                {
                    statsSummery.IncrementTotalMaintenance1PerMonth(maintenancePerMonth);
                }

                if (maintainedEntity.Maintenance.Costs.Product.Id.Equals(maintT2.Id))
                {
                    statsSummery.IncrementTotalMaintenance2PerMonth(maintenancePerMonth);
                }

                if (maintainedEntity.Maintenance.Costs.Product.Id.Equals(maintT3.Id))
                {
                    statsSummery.IncrementTotalMaintenance3PerMonth(maintenancePerMonth);
                }
            }

            if (selectedEntity is IElectricityConsumingEntity electricityConsumingEntity)
            {
                var electricityConsumerReadonly = electricityConsumingEntity.ElectricityConsumer;
                if (electricityConsumerReadonly.HasValue)
                {
                    var powerRequired = electricityConsumerReadonly.Value.PowerRequired;
                    statsSummery.IncrementTotalPowerRequired(powerRequired);
                }
            }

            if (selectedEntity is IEntityWithWorkers entityWithWorkers)
            {
                statsSummery.IncrementTotalWorkersAssigned(entityWithWorkers.WorkersNeeded);
            }

            if (selectedEntity is IComputingConsumingEntity computingConsumer)
            {
                statsSummery.IncrementComputingRequired(computingConsumer.ComputingRequired);
            }
        }

        return statsSummery;
    }
}