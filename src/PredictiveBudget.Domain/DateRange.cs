using System;
using System.Collections.Generic;
using System.Text;

namespace PredictiveBudget.Domain;

public readonly record struct DateRange(DateOnly Start, DateOnly End)
{
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}