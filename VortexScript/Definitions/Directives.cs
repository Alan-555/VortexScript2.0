using System.Reflection;
using VortexScript.Vortex;

namespace VortexScript.Definitions;

static class Directives{
    
    public static DirectiveDefinition<bool> DIR_BufferMode = new(false);

    public static FieldInfo GetDirectiveField(string dirName, out Type type)
    {
         type = null;

        // Use reflection to get the current class (or whatever class you're inspecting).
        // In this example, I'm assuming the static fields are in this class. You can change the target type if needed.
        Type targetType = typeof(Directives);

        // Retrieve the static public field by its name using reflection
        FieldInfo fieldInfo = targetType.GetField(dirName, BindingFlags.Public | BindingFlags.Static);

        // If the field is found and it is generic, process further
        if (fieldInfo != null)
        {
            // Get the type of the field
            Type fieldType = fieldInfo.FieldType;

            // Check if the field type is a generic type
            if (fieldType.IsGenericType)
            {
                // Get the generic arguments of the field's type
                Type[] genericArguments = fieldType.GetGenericArguments();

                // Assuming it's a generic type with one type argument, set the dataType
                if (genericArguments.Length == 1)
                {
                    type = (genericArguments[0]);  // Get the T type from the generic field
                }
                else
                {
                    throw new InvalidOperationException("The field has more than one generic argument.");
                }
            }
            else
            {
                throw new InvalidOperationException("The field is not of a generic type.");
            }
        }
        else
        {
            throw new UnknownNameError($"{dirName[4..]}");
        }

        return fieldInfo;
    }
}


class DirectiveDefinition<T>
{
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

}