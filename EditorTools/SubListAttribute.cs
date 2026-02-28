using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class SubListAttribute : PropertyAttribute
{
    public string Name;
    public bool StartClosed;

    public SubListAttribute(string name, bool startClosed = false)
    {
        Name = name;
        StartClosed = startClosed;
    }
}
