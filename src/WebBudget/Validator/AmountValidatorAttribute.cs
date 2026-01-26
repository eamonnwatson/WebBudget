using System.ComponentModel.DataAnnotations;

namespace WebBudget.Validator;

public class AmountValidatorAttribute : ValidationAttribute
{

    public override bool IsValid(object? value)
    {
        if (value == null)
            return true;

        decimal val = (decimal)value;

        if (val == 0) return false;

        return true;
    }
}
