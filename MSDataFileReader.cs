/*
 * Original author: Matthew Monroe <matthew.monroe .at. pnnl.gov>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;

namespace pwiz.ProteowizardWrapper
{
	public class MSDataFileReader : MsDataFileImpl
	{

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="msDataFile">MSData object</param>
		public MSDataFileReader(MSData msDataFile) : base(msDataFile)
        {
			// Call the base class constructor
			;
        }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="path">Data file path</param>
		public MSDataFileReader(string path) : base(path)
        {
			// Call the base class constructor
			;
        }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="path">Data file path</param>
		/// <param name="sampleIndex">Sample Index to select within the data file</param>
		public MSDataFileReader(string path, int sampleIndex) : base (path, sampleIndex)
        {
			// Call the base class constructor
			;       
        }

		public CVParamList GetChromatogramCVParams(int chromIndex)
		{
			return ChromatogramList.chromatogram(chromIndex).cvParams;
		}

		public Chromatogram GetChromatogramObject(int chromIndex)
		{
			return ChromatogramList.chromatogram(chromIndex, true);
		}

		public CVParamList GetSpectrumCVParams(int scanIndex)
		{
			return SpectrumList.spectrum(scanIndex).cvParams;
		}

		public Spectrum GetSpectrumObject(int scanIndex)
		{
			return SpectrumList.spectrum(scanIndex, true);
		}


	}
}
