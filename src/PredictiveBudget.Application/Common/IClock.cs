using System;
using System.Collections.Generic;
using System.Text;

namespace PredictiveBudget.Application.Common;

public interface IClock
{
    DateOnly Today();
}
