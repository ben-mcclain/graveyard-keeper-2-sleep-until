using Mono.Cecil;
using SleepUntil;

int cases = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
void Test(string name, Action body) { body(); cases++; Console.WriteLine("PASS " + name); }

Test("All six weekdays and next-morning mode across every quarter-hour", () =>
{
    int scenarios = 0;
    for (int day = 1; day <= 6; day++)
    for (int minute = 0; minute < 1440; minute += 15)
    for (int mode = 0; mode <= 6; mode++)
    {
        int? weekday = mode == 0 ? null : mode;
        var scheduler = new SleepSchedule(day, minute / 1440.0, weekday);
        int start = day * 1440 + minute;
        int expected = day * 1440 + 360;
        if (expected <= start) expected += 1440;
        while (weekday.HasValue && ((expected / 1440 - 1) % 6 + 1) != weekday) expected += 1440;
        double preview = SleepSchedule.PreviewDays(day, minute / 1440.0, 1, weekday);
        Check(Math.Abs(preview * 1440 - (expected - start)) < 1e-7, "Preview differs from next future dawn");
        for (int now = start + 1; now <= expected; now++)
        {
            bool wake = scheduler.Observe(now / 1440, (now % 1440) / 1440.0);
            Check(wake == (now == expected), $"Wrong wake {day}/{minute}/{mode}: {now} vs {expected}");
        }
        Check(!scheduler.Observe(expected / 1440, .251), "Duplicate completion");
        scenarios++;
    }
    Console.WriteLine($"  {scenarios} complete minute-step sleep simulations checked");
});

Test("One minute before dawn wakes this morning", () =>
{
    var s = new SleepSchedule(6, 359.0 / 1440, 6);
    Check(s.Observe(6, .25), "Missed upcoming same-day dawn");
});
Test("Starting exactly at dawn means next occurrence", () =>
{
    var s = new SleepSchedule(6, .25, 6);
    Check(!s.Observe(6, .250001), "Immediate wake");
    Check(!s.Observe(7, .25), "Wrong weekday");
    Check(s.Observe(12, .25), "Missed next week");
});
Test("Frame overshoot beyond dawn still wakes", () =>
{
    var s = new SleepSchedule(3, .2499, 3);
    Check(s.Observe(3, .2511), "Missed frame crossing");
});
Test("Midnight changes date but does not wake", () =>
{
    var s = new SleepSchedule(6, .99, null);
    Check(!s.Observe(7, .001), "Woke at midnight");
    Check(s.Observe(7, .25), "Missed next morning");
});
Test("Scripted date jump skips the requested weekday without wrong-day wake", () =>
{
    var s = new SleepSchedule(1, .8, 2);
    Check(!s.Observe(4, .001), "Jump woke sleeper");
    Check(!s.Observe(4, .26), "Wrong-day morning woke sleeper");
    Check(s.Observe(8, .26), "Requested weekday not recognized later");
});
Test("Scripted next-day override preview counts elapsed time, not skipped dates", () =>
{
    Check(Math.Abs(SleepSchedule.PreviewDays(1, .75, 3, 4) - .5) < 1e-9, "Incorrect override preview");
    Check(Math.Abs(SleepSchedule.PreviewDays(1, .75, 3, 2) - 4.5) < 1e-9, "Incorrect wrapped override preview");
});
Test("Backward date is rejected for runtime cancellation", () =>
{
    try { new SleepSchedule(8, .8, 2).Observe(7, .9); throw new Exception("Accepted backward date"); }
    catch (InvalidOperationException) { }
});
Test("Invalid calendar inputs rejected", () =>
{
    foreach (double time in new[] { -1.0, 1.0, double.NaN, double.PositiveInfinity })
    {
        try { _ = new SleepSchedule(1, time, null); throw new Exception("Accepted invalid time"); }
        catch (ArgumentOutOfRangeException) { }
    }
    foreach (int day in new[] { 0, 7 })
    {
        try { _ = new SleepSchedule(1, .5, day); throw new Exception("Accepted invalid weekday"); }
        catch (ArgumentOutOfRangeException) { }
    }
});

if (args.Length > 0)
{
    using var assembly = AssemblyDefinition.ReadAssembly(args[0]);
    TypeDefinition Type(string name) => assembly.MainModule.Types.Single(t => t.FullName == name);
    MethodDefinition Method(string type, string method) => Type(type).Methods.Single(m => m.Name == method);
    Test("Uploaded game: exact sleep-start parameter contract", () =>
    {
        var m = Method("EnergySystem", "StartSleeping");
        Check(m.ReturnType.FullName == "System.Void", "Changed return type");
        Check(string.Join(",", m.Parameters.Select(p => p.Name)) == "onSleepEndedByEnergyCallback,onSleepDidNotStartedCallback,sleepWithoutSavingGame,sleepWithMaxEnergy,sleepAnimType,sleepDuration", "Changed sleep-start parameters");
        Check(m.Parameters[3].ParameterType.FullName == "System.Boolean", "Changed full-energy flag");
    });
    Test("Uploaded game: bed flow continuation contracts", () =>
    {
        const string t = "GK2.FlowCanvasNodes.Flow_GoToSleep";
        Check(Method(t, "GoToSleep").Parameters.Single().ParameterType.FullName == "FlowCanvas.Flow", "Changed flow argument");
        Check(Method(t, "ResolveAnimType").ReturnType.FullName == "SleepAnimType", "Changed animation resolver");
        foreach (string field in new[] { "out", "onFinished", "onFinishedIfNoNeedToSleep" })
            Check(Type(t).Fields.Single(f => f.Name == field).FieldType.FullName == "FlowCanvas.FlowOutput", "Changed continuation " + field);
    });
    Test("Uploaded game: energy restoration retains a duration-controlled wake gate", () =>
    {
        Check(Type("EnergySystem").Fields.Single(f => f.Name == "remainingSleepTime").FieldType.FullName == "System.Single", "Changed duration field");
        var il = Method("EnergySystem", "RestoreEnergyWhileSleeping").Body.Instructions;
        Check(il.Count(i => i.Operand is FieldReference f && f.Name == "remainingSleepTime") >= 3, "Duration no longer read/written");
        Check(il.Count(i => i.Operand is MethodReference m && m.Name == "StopSleeping") == 1, "Changed wake path");
    });
    Test("Uploaded game: wake still saves and calendar tick precedes completion checks", () =>
    {
        var il = Method("EnergySystem", "StopSleeping").Body.Instructions;
        Check(il.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "SaveSystem" && m.Name == "Save"), "Save path changed");
        var tick = Method("EnvironmentEngine", "CustomUpdate").Body.Instructions;
        int restore = tick.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "HandleTimeOfDayChanged");
        int rollover = tick.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "AddToDay");
        Check(restore >= 0 && rollover > restore, "Tick order changed; re-review hook placement");
    });
    Test("Uploaded game: six-day calendar and dawn constant", () =>
    {
        Check((int)Type("EnvironmentData").Fields.Single(f => f.Name == "DAYS_IN_WEEK").Constant == 6, "Changed calendar");
        Check(Method("EnvironmentEngine", "GetTimeOfDayType").Body.Instructions.Any(i => i.Operand is float v && v == .25f), "Dawn changed");
    });
    Console.WriteLine("Game assembly MVID: " + assembly.MainModule.Mvid);
}
Console.WriteLine($"PASS: {cases} test groups. Unity runtime and actual save/load behavior require in-game testing.");
if (args.Length > 1)
{
    using var plugin = AssemblyDefinition.ReadAssembly(args[1]);
    Check(!plugin.MainModule.AssemblyReferences.Any(r => r.Name == "System.IO.Compression"), "Plugin unexpectedly depends on conflicting compression API");
    Console.WriteLine("Plugin references: " + string.Join(", ", plugin.MainModule.AssemblyReferences.Select(r => r.Name)));
    Console.WriteLine("Plugin dependency audit passed; no compression API reference.");
}
