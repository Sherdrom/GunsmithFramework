namespace GunsmithFramework
{
    internal static class GunsmithHookArgs
    {
        public static T? FindArg<T>(IEnumerable<object?> args) where T : class
        {
            foreach (object? arg in args)
            {
                if (arg is T value)
                {
                    return value;
                }
            }
            return null;
        }

        public static string? FindStringArg(IReadOnlyList<object?> args, int stringIndex)
        {
            int index = 0;
            foreach (object? arg in args)
            {
                if (arg is string value)
                {
                    if (index == stringIndex)
                    {
                        return value;
                    }
                    index++;
                }
            }
            return null;
        }

        public static int FindIntArg(IReadOnlyList<object?> args, int intIndex, int defaultValue = 0)
        {
            int index = 0;
            foreach (object? arg in args)
            {
                int? number = arg switch
                {
                    int intValue => intValue,
                    double doubleValue => (int)doubleValue,
                    float floatValue => (int)floatValue,
                    _ => null
                };

                if (number.HasValue)
                {
                    if (index == intIndex)
                    {
                        return number.Value;
                    }
                    index++;
                }
            }
            return defaultValue;
        }

        public static float FindFloatArg(IReadOnlyList<object?> args, int numberIndex)
        {
            int index = 0;
            foreach (object? arg in args)
            {
                float? number = NumberArg(arg);
                if (number.HasValue)
                {
                    if (index == numberIndex)
                    {
                        return number.Value;
                    }
                    index++;
                }
            }
            return 0.0f;
        }

        public static bool TryFindFloatArg(IReadOnlyList<object?> args, int numberIndex, out float value)
        {
            int index = 0;
            foreach (object? arg in args)
            {
                float? number = NumberArg(arg);
                if (number.HasValue)
                {
                    if (index == numberIndex)
                    {
                        value = number.Value;
                        return float.IsFinite(value);
                    }
                    index++;
                }
            }

            value = 0.0f;
            return false;
        }

        private static float? NumberArg(object? arg)
            => arg switch
            {
                int intValue => intValue,
                double doubleValue => (float)doubleValue,
                float floatValue => floatValue,
                _ => null
            };
    }
}
