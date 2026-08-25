using System;
using System.Collections.Generic;

namespace persona.Models;

public readonly record struct Suggestion(
    Guid Target,
    string TargetName,
    string ModDir,
    string ModName,
    IReadOnlyList<string> Items,
    bool Enable,
    int Priority,
    bool Done);
