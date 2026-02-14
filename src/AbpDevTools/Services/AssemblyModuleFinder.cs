using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AbpDevTools.Services;

[RegisterTransient]
public class AssemblyModuleFinder
{
    public ModuleTypeInfo? FindAbpModuleClass(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var metadataReader = peReader.GetMetadataReader();

            var moduleTypes = new List<ModuleTypeInfo>();
            
            foreach (var typeDefHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                var typeName = metadataReader.GetString(typeDef.Name);
                var namespaceName = metadataReader.GetString(typeDef.Namespace);
                
                if (string.IsNullOrEmpty(namespaceName) || typeName.StartsWith("<"))
                {
                    continue;
                }

                var baseTypes = GetBaseTypeChain(metadataReader, typeDef);
                
                if (baseTypes.Any(bt => bt.EndsWith(".AbpModule") || bt == "AbpModule" || bt.EndsWith(".Module") || bt == "Module"))
                {
                    return new ModuleTypeInfo
                    {
                        Name = typeName,
                        Namespace = namespaceName,
                        FullName = $"{namespaceName}.{typeName}",
                        AssemblyPath = assemblyPath
                    };
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public List<ModuleTypeInfo> FindAllAbpModuleClasses(string assemblyPath)
    {
        var modules = new List<ModuleTypeInfo>();
        
        if (!File.Exists(assemblyPath))
        {
            return modules;
        }

        try
        {
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var metadataReader = peReader.GetMetadataReader();

            foreach (var typeDefHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                var typeName = metadataReader.GetString(typeDef.Name);
                var namespaceName = metadataReader.GetString(typeDef.Namespace);
                
                if (string.IsNullOrEmpty(namespaceName) || typeName.StartsWith("<"))
                {
                    continue;
                }

                var baseTypes = GetBaseTypeChain(metadataReader, typeDef);
                
                if (baseTypes.Any(bt => bt.EndsWith(".AbpModule") || bt == "AbpModule"))
                {
                    modules.Add(new ModuleTypeInfo
                    {
                        Name = typeName,
                        Namespace = namespaceName,
                        FullName = $"{namespaceName}.{typeName}",
                        AssemblyPath = assemblyPath
                    });
                }
            }
        }
        catch
        {
        }

        return modules;
    }

    private List<string> GetBaseTypeChain(MetadataReader reader, TypeDefinition typeDef)
    {
        var baseTypes = new List<string>();
        var currentTypeDef = typeDef;

        for (int i = 0; i < 20; i++)
        {
            if (currentTypeDef.BaseType.IsNil)
            {
                break;
            }

            string? baseTypeName = null;
            
            if (currentTypeDef.BaseType.Kind == HandleKind.TypeReference)
            {
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)currentTypeDef.BaseType);
                var ns = reader.GetString(typeRef.Namespace);
                var name = reader.GetString(typeRef.Name);
                baseTypeName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
            else if (currentTypeDef.BaseType.Kind == HandleKind.TypeDefinition)
            {
                var baseTypeDef = reader.GetTypeDefinition((TypeDefinitionHandle)currentTypeDef.BaseType);
                var ns = reader.GetString(baseTypeDef.Namespace);
                var name = reader.GetString(baseTypeDef.Name);
                baseTypeName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }

            if (baseTypeName != null)
            {
                baseTypes.Add(baseTypeName);

                if (baseTypeName == "System.Object")
                {
                    break;
                }

                if (currentTypeDef.BaseType.Kind == HandleKind.TypeDefinition)
                {
                    currentTypeDef = reader.GetTypeDefinition((TypeDefinitionHandle)currentTypeDef.BaseType);
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        return baseTypes;
    }
}
