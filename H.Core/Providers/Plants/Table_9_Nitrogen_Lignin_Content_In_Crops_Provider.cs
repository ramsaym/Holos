﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using H.Core.Converters;
using H.Core.Enumerations;
using H.Infrastructure;
using H.Content;

namespace H.Core.Providers.Plants
{
    /// <summary>
    /// Table 9. Default values for nitrogen and lignin contents in crops for the IPCC (2019) Tier 2 steady-state method (iterated)
    /// </summary>
    public class Table_9_Nitrogen_Lignin_Content_In_Crops_Provider
    {
        #region Fields

        private readonly CropTypeStringConverter _cropTypeStringConverter; 
        
        #endregion

        #region Constructors

        /// <summary>
        /// Creates a crop string converter and reads the csv file.
        /// </summary>
        public Table_9_Nitrogen_Lignin_Content_In_Crops_Provider()
        {
            _cropTypeStringConverter = new CropTypeStringConverter();

            this.Data = this.ReadFile();
        }

        #endregion

        #region Properties

        /// <summary>
        /// List contains instances where each entry corresponds to data from a line in the csv file.
        /// </summary>
        List<Table_9_Nitrogen_Lignin_Content_In_Crops_Data> Data { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Takes a croptype and returns an instance containing data related to that crop.
        /// </summary>
        /// <param name="cropType">The type of crop for which we need the required data.</param>
        /// <returns>An instance for Table_9_Nitrogen_Lignin_Content_In_Crops_Data containing various values including nitrogen and lingin content values. Returns null if nothing found.
        /// For each data instance Lignin and Nitrogen content = Proportion of Carbon content. Moisture content = %</returns>
        public Table_9_Nitrogen_Lignin_Content_In_Crops_Data GetDataByCropType(CropType cropType)
        {
            if (cropType == CropType.Fallow)
            {
                return new Table_9_Nitrogen_Lignin_Content_In_Crops_Data();
            }

            var lookupType = cropType;

            if (cropType == CropType.Flax)
            {
                lookupType = CropType.FlaxSeed;
            }
            if (cropType == CropType.FieldPeas)
            {
                lookupType = CropType.DryFieldPeas;
            }

            Table_9_Nitrogen_Lignin_Content_In_Crops_Data data = this.Data.Find(x => x.CropType == lookupType);

            if (data != null)
            {
                return data;
            }
            else
            {
                Trace.TraceError($"{nameof(Table_9_Nitrogen_Lignin_Content_In_Crops_Provider)}.{nameof(Table_9_Nitrogen_Lignin_Content_In_Crops_Provider.GetDataByCropType)}" +
                    $" could not find Crop Type: {cropType} in the available crop data. Returning 0.");

                return new Table_9_Nitrogen_Lignin_Content_In_Crops_Data();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Reads the csv file and returns a list of instances for every crop in that file.
        /// </summary>
        /// <returns>A list containing NitrogenLinginInCropsForSteadyStateMethodData instances. Each instance corresponds to a crop and a line in the csv.</returns>
        private List<Table_9_Nitrogen_Lignin_Content_In_Crops_Data> ReadFile()
        {
            var cropInstances = new List<Table_9_Nitrogen_Lignin_Content_In_Crops_Data>();
            var cultureInfo = InfrastructureConstants.EnglishCultureInfo;

            IEnumerable<string[]> fileLines = CsvResourceReader.GetFileLines(CsvResourceNames.NitrogenLinginContentsInSteadyStateMethods);

            foreach (string[] line in fileLines.Skip(1).Take(58))
            {
                if (line.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                CropType cropType = _cropTypeStringConverter.Convert(line[1]);
                var intercept = double.Parse(line[2].ParseUntilOrDefault(), cultureInfo);
                var slope = double.Parse(line[3].ParseUntilOrDefault(), cultureInfo);
                var rst = double.Parse(line[4], cultureInfo);
                var nitrogenContent = double.Parse(line[5], cultureInfo);
                var ligninContent = double.Parse(line[6], cultureInfo);
                var moistureContent = double.Parse(line[7], cultureInfo);

                cropInstances.Add(new Table_9_Nitrogen_Lignin_Content_In_Crops_Data 
                {
                    CropType = cropType,
                    InterceptValue = intercept,
                    SlopeValue = slope,
                    RSTRatio = rst,
                    NitrogenContentResidues = nitrogenContent,
                    LigninContentResidues = ligninContent,
                    MoistureContent = moistureContent,
                });
            }

            return cropInstances;
        }

        #endregion
    }
}
