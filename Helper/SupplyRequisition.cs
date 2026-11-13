using CarCareTracker.Models;

namespace CarCareTracker.Helper;

public static class SupplyRequisition
{
    public static SupplyUsageHistory Apply(SupplyRecord supply, decimal quantity, DateTime date, string description)
    {
        var cost = supply.Quantity == 0 ? 0 : quantity * (supply.Cost / supply.Quantity);
        supply.Quantity -= quantity;
        supply.Cost = decimal.Round(supply.Cost - cost, 3);
        supply.RequisitionHistory.Add(new SupplyUsageHistory { Id = supply.Id, Date = date, Description = description, Quantity = quantity, Cost = cost });
        return new SupplyUsageHistory { Id = supply.Id, Date = date, Description = supply.Description, PartNumber = supply.PartNumber, Quantity = quantity, Cost = cost };
    }
}
