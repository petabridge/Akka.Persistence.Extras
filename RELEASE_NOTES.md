#### 0.2.0 February 15 2019 ####
Minor, but breaking change release of Akka.Persistence.Extras.

* Changed targets to .NET 4.5 and .NET Standard 1.6, in order to temporarily bring the project inline with Akka.NET's targets. We will move back to targeting .NET Standard 2.0 eventually.
* Removed all dependencies on `System.ValueTuple` as this caused compilation issues on Linux with .NET Framework 4.5.