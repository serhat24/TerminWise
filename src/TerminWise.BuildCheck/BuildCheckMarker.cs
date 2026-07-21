namespace TerminWise.BuildCheck;

/// <summary>
/// Phase 0 build-check placeholder — its only job is to give CI something real to
/// compile and test before any application code exists. The actual Clean Architecture
/// projects (Domain, Application, Infrastructure, Api, ...) are introduced in Phase 1,
/// at which point this project and its test are removed.
/// </summary>
public static class BuildCheckMarker
{
    public const string Phase = "0";

    public static string Describe() => $"TerminWise build-check placeholder (phase {Phase}).";
}
