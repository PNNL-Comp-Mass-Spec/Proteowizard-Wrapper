using System.Collections.Generic;
using System.Linq;

namespace pwiz.ProteowizardWrapper
{
    public static class CVParamUtilities
    {
        /// <summary>
        /// CVIDs used by the unit tests
        /// </summary>
        public enum CVIDs
        {
            MS_scan_start_time = pwiz.CLI.cv.CVID.MS_scan_start_time,                           // 1000016
            MS_TIC = pwiz.CLI.cv.CVID.MS_TIC,                                                   // 1000285
            MS_scan_window_upper_limit = pwiz.CLI.cv.CVID.MS_scan_window_upper_limit,           // 1000500
            MS_scan_window_lower_limit = pwiz.CLI.cv.CVID.MS_scan_window_lower_limit,           // 1000501
            MS_base_peak_m_z = pwiz.CLI.cv.CVID.MS_base_peak_m_z,                               // 1000504
            MS_base_peak_intensity = pwiz.CLI.cv.CVID.MS_base_peak_intensity,                   // 1000505
            MS_filter_string = pwiz.CLI.cv.CVID.MS_filter_string,                               // 1000512
            MS_ion_injection_time = pwiz.CLI.cv.CVID.MS_ion_injection_time,                     // 1000927
            MS_ion_mobility_drift_time = pwiz.CLI.cv.CVID.MS_ion_mobility_drift_time,           // 1002476
            MS_data_independent_acquisition = pwiz.CLI.cv.CVID.MS_data_independent_acquisition  // 1003215
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
                return query[0].Value;
            }

            return string.Empty;
        }

        // ReSharper disable once UnusedMember.Global
        public static int GetCvParamValueInt(IEnumerable<CVParamData> cvParams, CVIDs cvId)
        {
            var query = (from item in cvParams where item.CVId == (int)cvId select item).ToList();

            if (query.Count > 0)
            {
                if (int.TryParse(query[0].Value, out var value))
                    return value;
            }

            return 0;
        }

        public static double GetCvParamValueDbl(IEnumerable<CVParamData> cvParams, CVIDs cvId)
        {
            var query = (from item in cvParams where item.CVId == (int)cvId select item).ToList();

            if (query.Count > 0)
            {
                if (double.TryParse(query[0].Value, out var value))
                    return value;
            }

            return 0;
        }
    }
}
