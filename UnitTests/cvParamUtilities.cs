using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.ProteowizardWrapper;

namespace ProteowizardWrapperUnitTests
{
    internal class cvParamUtilities
    {
        public enum CVIDs
        {
            MS_scan_start_time = 1000016,
            MS_scan_window_upper_limit = 1000500,
            MS_scan_window_lower_limit = 1000501,
            MS_filter_string = 1000512,
            MS_ion_injection_time = 1000927,
            MS_TIC = 1000285,
            MS_base_peak_m_z = 1000504,
            MS_base_peak_intensity = 1000505,
            MS_ion_mobility_drift_time = 1002476
        }

        public static double CheckNull(double? value)
        {
            if (value == null)
                return 0;

            return (double)value;
        }

        public static string GetCvParamValue(IEnumerable<CVParamData> cvParams, CVIDs cvId)
        {
            var query = (from item in cvParams where item.CVId == (int)cvId select item).ToList();

            if (query.Count > 0)
            {
                return query.First().Value;
            }

            return string.Empty;
        }

        // ReSharper disable once UnusedMember.Global
        public static int GetCvParamValueInt(IEnumerable<CVParamData> cvParams, CVIDs cvId)
        {
            var query = (from item in cvParams where item.CVId == (int)cvId select item).ToList();

            if (query.Count > 0)
            {
                if (Int32.TryParse(query.First().Value, out var value))
                    return value;
            }

            return 0;
        }

        public static double GetCvParamValueDbl(IEnumerable<CVParamData> cvParams, CVIDs cvId)
        {
            var query = (from item in cvParams where item.CVId == (int)cvId select item).ToList();

            if (query.Count > 0)
            {
                if (Double.TryParse(query.First().Value, out var value))
                    return value;
            }

            return 0;
        }

    }
}
