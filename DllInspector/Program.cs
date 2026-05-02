using Mono.Cecil;
using Mono.Cecil.Cil;

var dllPath = @"c:\Users\benja\OneDrive\Desktop\coding\MineFacv1\MineMogul\MineMogul_Data\Managed\Assembly-CSharp.dll";
var asm = AssemblyDefinition.ReadAssembly(dllPath);

var targets = new[] { "BaseHeldTool", "ConveyorSplitterT2", "PlayerController", "PlayerInventory", "ToolBuilder", "InventoryItem", "HeldItem" };

foreach (var type in asm.MainModule.Types.OrderBy(t => t.Name))
{
    if (!targets.Any(k => type.Name.Equals(k, StringComparison.OrdinalIgnoreCase))) continue;
    Console.WriteLine($"\n=== {type.FullName} (base: {type.BaseType?.Name}) ===");
    foreach (var iface in type.Interfaces)
        Console.WriteLine($"  impl: {iface.InterfaceType.Name}");
    foreach (var field in type.Fields)
        Console.WriteLine($"  field: [{field.FieldType.Name}] {field.Name}");
    foreach (var prop in type.Properties)
        Console.WriteLine($"  prop:  [{prop.PropertyType.Name}] {prop.Name}");
    foreach (var method in type.Methods.Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")))
    {
        var parms = string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name + " " + p.Name));
        Console.WriteLine($"  method: {method.Name}({parms})");
    }
}


foreach (var type in asm.MainModule.Types.OrderBy(t => t.Name))
{
    if (!targets.Any(k => type.Name.Equals(k, StringComparison.OrdinalIgnoreCase))) continue;
    Console.WriteLine($"\n=== {type.FullName} ===");
    foreach (var field in type.Fields)
        Console.WriteLine($"  field: [{field.FieldType.Name}] {field.Name}");
    foreach (var method in type.Methods)
    {
        var parms = string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name + " " + p.Name));
        Console.WriteLine($"  method: {method.Name}({parms})");
        if (method.Name == "Initialize" && method.HasBody)
        {
            foreach (var instr in method.Body.Instructions)
                if (instr.Operand != null)
                    Console.WriteLine($"    IL: {instr.OpCode,12}  {instr.Operand}");
        }
    }
}

