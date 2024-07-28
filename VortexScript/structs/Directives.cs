using System.Reflection;

namespace Vortex
{
    class DirectiveDefinition<T>
    {
        public static DirectiveDefinition<bool> DIR_BufferMode = new(false);
        public DataType dataType;
        public T value;

        public DirectiveDefinition(T value)
        {
            if (typeof(T) == typeof(bool))
            {
                dataType = DataType.Bool;
            }
            else if (typeof(T) == typeof(double))
            {
                dataType = DataType.Number;
            }
            else if (typeof(T) == typeof(string))
            {
                dataType = DataType.String;
            }
            this.value = value;
        }

        public static FieldInfo GetDirectiveField(string dirName, out DataType dataType)
        {
            // Initialize the out parameter
            dataType = default(DataType);

            // Get the type of DirectiveDefinition<T>
            var type = typeof(DirectiveDefinition<>);

            // Loop through possible type parameters (bool, double)
            foreach (var fieldType in new[] { typeof(bool), typeof(double) })
            {
                // Get the specific type of DirectiveDefinition<T>
                var specificType = type.MakeGenericType(fieldType);

                // Get the static field by name
                var field = specificType.GetField(dirName, BindingFlags.Static | BindingFlags.Public);

                if (field != null)
                {
                    // Get the value of the static field
                    var directiveInstance = field.GetValue(null);

                    // Get the dataType field from the instance
                    var dataTypeField = specificType.GetField("dataType", BindingFlags.Instance | BindingFlags.Public);

                    if (dataTypeField != null)
                    {
                        // Set the out parameter
                        dataType = (DataType)dataTypeField.GetValue(directiveInstance)!;
                    }

                    // Return the FieldInfo
                    return field;
                }
            }
            throw new UnknownNameError($"{dirName}'");
        }

    }
}