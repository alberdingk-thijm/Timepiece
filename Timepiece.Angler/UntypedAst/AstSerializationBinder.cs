using Newtonsoft.Json.Serialization;
using Timepiece.Angler.UntypedAst.AstFunction;

namespace Timepiece.Angler.UntypedAst;

public class AstSerializationBinder : ISerializationBinder
{
  public Type BindToType(string? assemblyName, string typeName)
  {
    return typeName switch
    {
      "Finally" => typeof(Finally),
      "Globally" => typeof(Globally),
      "Until" => typeof(Until),
      _ => TypeParsing.ParseType(typeName).MakeType()
    };
  }

  public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
  {
    assemblyName = null;
    typeName = serializedType.Name;
  }
}
