﻿using H.Core.Calculators.Nitrogen;
using H.Core.Models.LandManagement.Fields;
using H.Core.Models;
using System;
using H.Core.Enumerations;

namespace H.Core.Calculators.Carbon
{
    public partial class ICBMSoilCarbonCalculator
    {
        #region Fields

        private readonly N2OEmissionFactorCalculator _singleYearNitrogenEmissionsCalculator = new N2OEmissionFactorCalculator();

        #endregion

        #region Properties

        public double OldPoolNitrogenRequirement { get; set; }

        #endregion

        #region Overrides

        protected override void SetOrganicNitrogenPoolStartState()
        {
            // Equation 2.6.1-3
            this.OrganicPool += ((this.CurrentYearResults.GetTotalOrganicNitrogenInYear() / this.CurrentYearResults.Area));
        }

        #endregion

        #region Public Methods

        public void CalculateNitrogenAtInterval(
           CropViewItem previousYearResults,
           CropViewItem currentYearResults,
           CropViewItem nextYearResults,
           Farm farm,
           int yearIndex)
        {
            this.CurrentYearResults = currentYearResults;
            this.PreviousYearResults = previousYearResults;
            this.YearIndex = yearIndex;
            this.Year = this.YearIndex + farm.CarbonModellingEquilibriumYear;

            var climateParameterOrManagementFactor = farm.Defaults.UseClimateParameterInsteadOfManagementFactor ? currentYearResults.ClimateParameter : currentYearResults.ManagementFactor;

            base.SetPoolStartStates(farm);

            base.MineralPool = this.CalculateMineralizedNitrogenFromDecompositionOfOldCarbon(
                oldPoolSoilCarbonAtPreviousInterval: previousYearResults.OldPoolSoilCarbon,
                oldPoolDecompositionRate: farm.Defaults.DecompositionRateConstantOldPool,
                climateParameter: climateParameterOrManagementFactor,
                oldCarbonNitrogen: farm.Defaults.OldPoolCarbonN);

            base.TotalInputsBeforeReductions();
            base.CalculateDirectEmissions(farm, currentYearResults);
            base.CalculateIndirectEmissions(farm, currentYearResults);
            base.AdjustPools();
            base.CloseNitrogenBudget(currentYearResults);

            base.CalculatePoolRatio();

            this.OldPoolNitrogenRequirement = this.CalculateNitrogenRequirementForCarbonTransitionFromYoungToOldPool(farm.Defaults.OldPoolCarbonN);

            // This is the first adjustment after the old pool demand has been determined
            base.AdjustPoolsAfterDemandCalculation(this.OldPoolNitrogenRequirement);

            this.CurrentYearResults.MicrobialPoolAfterOldPoolDemandAdjustment = base.MicrobePool;

            // Need to ensure we are passing in 100% of returns since the N uptake is before the harvest

            // Equation 2.6.7-12
            base.CropNitrogenDemand = this.CalculateCropNitrogenDemand(
                carbonInputFromProduct: currentYearResults.PlantCarbonInAgriculturalProduct,
                carbonInputFromStraw: currentYearResults.CarbonInputFromStraw,
                carbonInputFromRoots: currentYearResults.CarbonInputFromRoots,
                carbonInputFromExtraroots: currentYearResults.CarbonInputFromExtraroots,
                moistureContentOfCropFraction: currentYearResults.MoistureContentOfCrop,
                nitrogenConcentrationInTheProduct: currentYearResults.NitrogenContentInProduct,
                nitrogenConcentrationInTheStraw: currentYearResults.NitrogenContentInStraw,
                nitrogenConcentrationInTheRoots: currentYearResults.NitrogenContentInRoots,
                nitrogenConcentrationInExtraroots: currentYearResults.NitrogenContentInExtraroot,
                nitrogenFixation: farm.Defaults.DefaultNitrogenFixation,
                carbonConcentration: farm.Defaults.CarbonConcentration);

            // This is the second adjustment after the crop demand has been determined
            base.AdjustPoolsAfterDemandCalculation(base.CropNitrogenDemand);

            base.CurrentYearResults.MicrobialPoolAfterCropDemandAdjustment = base.MicrobePool;

            // Summation of emission must occur before balancing pools so nitrification can be calculated using total N2O-N emissions
            base.SumEmissions();

            base.BalancePools(farm.Defaults.MicrobeDeath);

            // Equation 2.6.9-30
            base.CurrentYearResults.TotalUptake = base.CropNitrogenDemand + this.OldPoolNitrogenRequirement;
            this.AssignFinalValues();
        }

        /// <summary>
        /// Equation 2.6.2-1
        ///
        /// Modified to to accept the total above ground residue nitrogen instead of calculating the above ground residue nitrogen in place here
        /// </summary>
        public double CalculateAboveGroundResidueNitrogenAtEquilibrium(
            double climateFactor,
            double youngPoolDecompositionRate,
            double totalAboveGroundResidueNitrogen)
        {
            var result = (totalAboveGroundResidueNitrogen * Math.Exp(-1 * youngPoolDecompositionRate * climateFactor)) / (1 - Math.Exp(-1 * youngPoolDecompositionRate * climateFactor));

            return result;
        }

        /// <summary>
        /// Equation 2.6.2-3
        /// </summary>
        public double CalculateAboveGroundResidueNitrogenForFieldAtInterval(
            double aboveGroundResidueNitrogenForFieldAtPreviousInterval,
            double aboveGroundResidueNitrogenForCropAtPreviousInterval,
            double climateManagementFactor,
            double decompositionRateYoungPool)
        {
            var result = (aboveGroundResidueNitrogenForFieldAtPreviousInterval + aboveGroundResidueNitrogenForCropAtPreviousInterval) * Math.Exp((-1) * decompositionRateYoungPool * climateManagementFactor);

            return result;
        }

        /// <summary>
        /// Equation 2.6.2-4
        ///
        /// Modified to to accept the total below ground residue nitrogen instead of calculating the below ground residue nitrogen in place here
        /// </summary>
        public double CalculateBelowGroundResidueNitrogenAtEquilibrium(double youngPoolDecompositionRate,
            double climateFactor,
            double totalBelowGroundResidueNitrogen)
        {
            var result = (totalBelowGroundResidueNitrogen * Math.Exp(-1 * youngPoolDecompositionRate * climateFactor)) / (1 - Math.Exp(-1 * youngPoolDecompositionRate * climateFactor));

            return result;
        }

        /// <summary>
        /// Equation 2.6.2-6
        /// </summary>
        public double CalculateBelowGroundResidueNitrogenForFieldAtInterval(
            double belowGroundResidueNitrogenForFieldAtPreviousInterval,
            double belowGroundResidueNitrogenForCropAtPreviousInterval,
            double climateManagementFactor,
            double decompositionRateYoungPool)
        {
            var result = (belowGroundResidueNitrogenForFieldAtPreviousInterval + belowGroundResidueNitrogenForCropAtPreviousInterval) * Math.Exp((-1) * decompositionRateYoungPool * climateManagementFactor);

            return result;
        }

        /// <summary>
        /// Equation 2.6.3-1
        ///
        /// The amount of manure N in the pool after decomposition has occurred.
        ///
        /// (kg N ha^-1)
        /// </summary>
        public double CalculateManureResiduePoolAtStartingPoint(
            double manureInputs,
            double decompositionRateConstantYoungPool,
            double climateParameter)
        {
            var result = (manureInputs * Math.Exp(-1 * decompositionRateConstantYoungPool * climateParameter)) / (1 - Math.Exp(-1 * decompositionRateConstantYoungPool * climateParameter));

            return result;
        }

        /// <summary>
        /// Equation 2.6.3-2
        ///
        /// The amount of manure N in the pool after decomposition has occurred.
        ///
        /// (kg N ha^-1)
        /// </summary>
        public double CalculateManureResiduePoolAtInterval(double manureResidueNitrogenPoolAtPreviousInterval,
            double manureInputsAtPreviousInterval,
            double decompositionRateYoungPool,
            double climateParameter)
        {
            var result = (manureResidueNitrogenPoolAtPreviousInterval + manureInputsAtPreviousInterval) * Math.Exp((-1) * decompositionRateYoungPool * climateParameter);

            return result;
        }

        /// <summary>
        /// Equation 2.6.4-1
        /// </summary>
        public double CalculateCropResiduesAtStartingPoint(
            double aboveGroundResidueNitrogenForCropAtStartingPoint,
            double belowGroundResidueNitrogenForCropAtStartingPoint,
            double decompositionRateConstantYoungPool,
            double climateParameter)
        {
            var result = (aboveGroundResidueNitrogenForCropAtStartingPoint + belowGroundResidueNitrogenForCropAtStartingPoint) -
                         (aboveGroundResidueNitrogenForCropAtStartingPoint + belowGroundResidueNitrogenForCropAtStartingPoint) * Math.Exp((-1) * decompositionRateConstantYoungPool * climateParameter);

            return result;
        }

        /// <summary>
        /// Equation 2.6.4-2
        /// </summary>
        public double CalculateCropResiduesAtInterval(
            double aboveGroundResidueNitrogenForFieldAtCurrentInterval,
            double aboveGroundResidueNitrogenForFieldAtPreviousInterval,
            double aboveGroundResidueNitrogenForCropAtPreviousInterval,
            double belowGroundResidueNitrogenForFieldAtCurrentInterval,
            double belowGroundResidueNitrogenForFieldAtPreviousInterval,
            double belowGroundResidueNitrogenForCropAtPreviousInterval)
        {
            var result = ((aboveGroundResidueNitrogenForFieldAtPreviousInterval + aboveGroundResidueNitrogenForCropAtPreviousInterval) - aboveGroundResidueNitrogenForFieldAtCurrentInterval) +
                         ((belowGroundResidueNitrogenForFieldAtPreviousInterval + belowGroundResidueNitrogenForCropAtPreviousInterval) - belowGroundResidueNitrogenForFieldAtCurrentInterval);

            return result;
        }

        /// <summary>
        /// Equation 2.6.4-3
        /// </summary>
        public double CalculateMineralizedNitrogenFromDecompositionOfOldCarbon(
            double oldPoolSoilCarbonAtPreviousInterval,
            double oldPoolDecompositionRate,
            double climateParameter,
            double oldCarbonNitrogen)
        {
            var result = (oldPoolSoilCarbonAtPreviousInterval - (oldPoolSoilCarbonAtPreviousInterval * Math.Exp((-1) * climateParameter * oldPoolDecompositionRate))) * oldCarbonNitrogen;

            return result;
        }

        /// <summary>
        /// Equation 2.6.8-7
        /// </summary>
        public double CalculateNitrogenRequirementForCarbonTransitionFromYoungToOldPool(double oldPoolSoilCarbon)
        {
            var result = (this.CurrentYearResults.OldPoolSoilCarbon - this.PreviousYearResults.OldPoolSoilCarbon) *
                         oldPoolSoilCarbon;

            return result;
        }

        #region Overrides

        protected override void SetCropResiduesStartState(Farm farm)
        {
            // Crop residue N inputs from crop are not adjusted later on, so we can display them at this point
            this.CurrentYearResults.AboveGroundNitrogenResidueForCrop = this.PreviousYearResults.CombinedAboveGroundResidueNitrogen;
            this.CurrentYearResults.BelowGroundResidueNitrogenForCrop = this.PreviousYearResults.CombinedBelowGroundResidueNitrogen;

            var climateParameterOrManagementFactor = farm.Defaults.UseClimateParameterInsteadOfManagementFactor ? this.CurrentYearResults.ClimateParameter : this.CurrentYearResults.ManagementFactor;

            if (this.YearIndex == 0)
            {
                // Calculate the above and below ground starting crop residue pools for the field (t = 0)
                this.AboveGroundResidueN = this.CalculateAboveGroundResidueNitrogenAtEquilibrium(
                    climateFactor: climateParameterOrManagementFactor,
                    youngPoolDecompositionRate: farm.Defaults.DecompositionRateConstantYoungPool, 
                    totalAboveGroundResidueNitrogen: this.PreviousYearResults.CombinedAboveGroundResidueNitrogen);

                this.BelowGroundResidueN = this.CalculateBelowGroundResidueNitrogenAtEquilibrium(
                    youngPoolDecompositionRate: farm.Defaults.DecompositionRateConstantYoungPool,
                    climateFactor: climateParameterOrManagementFactor,
                    totalBelowGroundResidueNitrogen: this.PreviousYearResults.CombinedBelowGroundResidueNitrogen);

                base.CropResiduePool = this.CalculateCropResiduesAtStartingPoint(
                    aboveGroundResidueNitrogenForCropAtStartingPoint: this.PreviousYearResults.CombinedAboveGroundResidueNitrogen,
                    belowGroundResidueNitrogenForCropAtStartingPoint: this.PreviousYearResults.CombinedBelowGroundResidueNitrogen,
                    decompositionRateConstantYoungPool: farm.Defaults.DecompositionRateConstantYoungPool,
                    climateParameter: climateParameterOrManagementFactor);
            }
            else
            {
                // Calculate the above and below ground crop residue pools for the field on subsequent years (t > 0)
                base.AboveGroundResidueN = this.CalculateAboveGroundResidueNitrogenForFieldAtInterval(
                    aboveGroundResidueNitrogenForFieldAtPreviousInterval: this.PreviousYearResults.AboveGroundResiduePool_AGresidueN,
                    aboveGroundResidueNitrogenForCropAtPreviousInterval: this.PreviousYearResults.CombinedAboveGroundResidueNitrogen,
                    climateManagementFactor: climateParameterOrManagementFactor,
                    decompositionRateYoungPool: farm.Defaults.DecompositionRateConstantYoungPool);

                base.BelowGroundResidueN = this.CalculateBelowGroundResidueNitrogenForFieldAtInterval(
                    belowGroundResidueNitrogenForFieldAtPreviousInterval: this.PreviousYearResults.BelowGroundResiduePool_BGresidueN,
                    belowGroundResidueNitrogenForCropAtPreviousInterval: this.PreviousYearResults.CombinedBelowGroundResidueNitrogen,
                    climateManagementFactor: climateParameterOrManagementFactor,
                    decompositionRateYoungPool: farm.Defaults.DecompositionRateConstantYoungPool);

                base.CropResiduePool = this.CalculateCropResiduesAtInterval(
                    aboveGroundResidueNitrogenForFieldAtCurrentInterval: base.CurrentYearResults.AboveGroundResiduePool_AGresidueN,
                    aboveGroundResidueNitrogenForFieldAtPreviousInterval: base.PreviousYearResults.AboveGroundResiduePool_AGresidueN,
                    aboveGroundResidueNitrogenForCropAtPreviousInterval: base.PreviousYearResults.AboveGroundNitrogenResidueForCrop,
                    belowGroundResidueNitrogenForFieldAtCurrentInterval: base.CurrentYearResults.BelowGroundResiduePool_BGresidueN,
                    belowGroundResidueNitrogenForFieldAtPreviousInterval: base.PreviousYearResults.BelowGroundResiduePool_BGresidueN,
                    belowGroundResidueNitrogenForCropAtPreviousInterval: base.PreviousYearResults.BelowGroundResidueNitrogenForCrop);
            }

            base.CurrentYearResults.CropResiduesBeforeAdjustment = base.CropResiduePool;
        }

        protected override void SetManurePoolStartState(Farm farm)
        {
            var climateParameterOrManagementFactor = farm.Defaults.UseClimateParameterInsteadOfManagementFactor ? this.CurrentYearResults.ClimateParameter : this.CurrentYearResults.ManagementFactor;

            if (this.YearIndex == 0)
            {
                this.CurrentYearResults.ManureResidueN = base.GetManureNitrogenResiduesForYear(farm, this.CurrentYearResults);

                this.ManurePool = this.CalculateManureResiduePoolAtStartingPoint(
                    manureInputs: this.CurrentYearResults.ManureResidueN,
                    decompositionRateConstantYoungPool: farm.Defaults.DecompositionRateConstantYoungPool,
                    climateParameter: climateParameterOrManagementFactor);
            }
            else
            {
                this.CurrentYearResults.ManureResidueN = base.GetManureNitrogenResiduesForYear(farm, this.PreviousYearResults);

                this.ManurePool = this.CalculateManureResiduePoolAtInterval(
                    manureResidueNitrogenPoolAtPreviousInterval: base.PreviousYearResults.ManureResiduePool_ManureN,
                    manureInputsAtPreviousInterval: this.CurrentYearResults.ManureResidueN,
                    decompositionRateYoungPool: farm.Defaults.DecompositionRateConstantYoungPool,
                    climateParameter: climateParameterOrManagementFactor);
            }
        }

        protected override void AdjustOrganicPool()
        {
            // Add in the manure N after all emissions (direct + indirect) have been calculated (and not before). This will avoid double counting emissions from manure N
            if (YearIndex == 0)
            {
                this.OrganicPool += this.ManurePool;
            }
            if (YearIndex > 0)
            {
                // Equation 2.6.7-8
                this.OrganicPool += (this.PreviousYearResults.ManureResiduePool_ManureN - this.ManurePool);
            }

            // Equation 2.6.7-9
            this.OrganicPool -= (this.N2O_NFromOrganicNitrogen + this.NO_NFromOrganicNitrogen);

            // Equation 2.6.7-10
            this.OrganicPool -= (this.N2O_NFromOrganicNitrogenLeaching + this.NO3FromOrganicNitrogenLeaching);

            // Equation 2.6.7-11
            this.OrganicPool -= (this.N2O_NOrganicNitrogenVolatilization + this.NH4FromOrganicNitogenVolatilized);
        }

        protected override void AssignFinalValues()
        {
            base.AssignFinalValues();

            this.CurrentYearResults.OldPoolNitrogenRequirement = this.OldPoolNitrogenRequirement;
        }

        #endregion

        #endregion
    }
}