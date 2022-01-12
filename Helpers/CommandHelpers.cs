﻿using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.Helpers;

public static class CommandHelpers
{
    public static bool TryExecute<T>(this IRelayCommand<T> command, T? parameter)
    {
        if (!command.CanExecute(parameter))
        {
            return false;
        }

        command.Execute(parameter);

        return true;
    }
}
