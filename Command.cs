using System;

namespace ServerTemplateCreator
{
    //public delegate void CommandMethod(string directory, string argumentsLine);

    public class Command
    {
        public Command(string name, string description, Action<string, string> commandMethod)
        {
            if (description is null || commandMethod is null || name is null)
                throw new NullReferenceException();
            (Name, Description, CommandMethod) = (name, description, commandMethod);
        }

        public readonly string Name;
        public readonly string Description;
        public readonly Action<string, string> CommandMethod;
    }
}