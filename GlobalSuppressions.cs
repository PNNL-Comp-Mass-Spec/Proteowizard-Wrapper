// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "RCS1075:Avoid empty catch clause that catches System.Exception.", Justification = "Ignore errors here", Scope = "member", Target = "~M:pwiz.ProteowizardWrapper.DependencyLoader.FindPwizPath~System.String")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~M:pwiz.ProteowizardWrapper.MsDataFileImpl.GetPwizSpectrum(System.Int32,System.Boolean)~pwiz.CLI.msdata.Spectrum")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~M:pwiz.ProteowizardWrapper.MsDataFileImpl.GetSpectrum(System.Int32,System.Boolean)~pwiz.ProteowizardWrapper.MsDataSpectrum")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~P:pwiz.ProteowizardWrapper.MsPrecursor.IsolationMz")]
[assembly: SuppressMessage("Usage", "RCS1146:Use conditional access.", Justification = "Leave as-is for readability", Scope = "member", Target = "~M:pwiz.ProteowizardWrapper.MsDataFileImpl.GetIonMobility(System.Int32)~pwiz.ProteowizardWrapper.Common.Chemistry.IonMobilityValue")]
[assembly: SuppressMessage("Usage", "RCS1146:Use conditional access.", Justification = "Leave as-is for readability", Scope = "member", Target = "~M:pwiz.ProteowizardWrapper.MsDataFileImpl.GetScanTimes~System.Double[]")]
[assembly: SuppressMessage("Usage", "RCS1146:Use conditional access.", Justification = "Leave as-is for readability", Scope = "member", Target = "~M:pwiz.ProteowizardWrapper.MsDataFileImpl.GetScanToIndexMapping~System.Collections.Generic.Dictionary{System.Int32,System.Int32}")]
[assembly: SuppressMessage("Usage", "RCS1146:Use conditional access.", Justification = "Leave as-is for readability", Scope = "member", Target = "~P:pwiz.ProteowizardWrapper.MsDataFileImpl.HasCombinedIonMobilitySpectra")]
[assembly: SuppressMessage("Usage", "RCS1146:Use conditional access.", Justification = "Leave as-is for readability", Scope = "member", Target = "~P:pwiz.ProteowizardWrapper.MsDataFileImpl.IsWatersLockmassCorrectionCandidate")]
[assembly: SuppressMessage("Usage", "RCS1146:Use conditional access.", Justification = "Leave as-is for readability", Scope = "member", Target = "~P:pwiz.ProteowizardWrapper.MsDataFileImpl.SpectrumList")]
