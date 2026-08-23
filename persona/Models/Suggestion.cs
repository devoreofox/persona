using System;

namespace persona.Models;

public readonly record struct Suggestion(
    Guid Target,
    string TargetName,
    string ModDir,
    string ModName,
    string Items,
    bool Enable,
    int Priority,
    bool Done);
