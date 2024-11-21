namespace WildFarmingRevival.ModSystem
{
    using Vintagestory.GameContent;
    //using System;
    using Vintagestory.API.Common.Entities;


    public interface ITreePoi : IAnimalFoodSource
    {
        string Stage { get; }
        // new bool IsSuitableFor(Entity entity, string[] diet); //ehm-93
        new bool IsSuitableFor(Entity entity, CreatureDiet diet); //ehm-93
    }
}
