using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BennerSmartInstaller.Services
{
    /// <summary>
    /// Leitura direta e somente-leitura de W_INDICEARTEFATOS (IDENTIFICADOR, HASH) por tipo, sem passar pelo
    /// ArtifactIndexerTask da plataforma (que também reindexa artefatos ainda não indexados).
    /// </summary>
    public class IndexDbReader
    {
        private readonly Type _dataReaderType;
        private readonly Type _paramCollType;
        private readonly MethodInfo _addParam;
        private readonly ConstructorInfo _ctor;

        public IndexDbReader(string binDir)
        {
            // Benner.Tecnologia.Common.dll não existe em WebApp\Bin: o tipo ParameterCollection é descoberto
            // pelo construtor do DataReader (assembly Business, já carregado pela plataforma).
            var businessAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "Benner.Tecnologia.Business", StringComparison.OrdinalIgnoreCase))
                ?? Assembly.LoadFrom(Path.Combine(binDir, "Benner.Tecnologia.Business.dll"));
            _dataReaderType = businessAsm.GetType("Benner.Tecnologia.Business.DataReader.DataReader", true);
            _ctor = _dataReaderType.GetConstructors()
                .FirstOrDefault(c => c.GetParameters().Length == 2 && c.GetParameters()[0].ParameterType == typeof(string)
                                     && c.GetParameters()[1].ParameterType.Name == "ParameterCollection");
            if (_ctor == null)
                throw new InvalidOperationException("Construtor DataReader(string, ParameterCollection) não localizado.");
            _paramCollType = _ctor.GetParameters()[1].ParameterType;
            // O assembly do tipo chama-se Benner.Tecnologia.Common2 e só expõe sobrecargas tipadas de Add.
            _addParam = _paramCollType.GetMethod("Add", new[] { typeof(string), typeof(int) });
            if (_addParam == null)
                throw new InvalidOperationException("ParameterCollection.Add(string, int) não localizado.");
        }

        public Dictionary<string, string> Read(int tipo)
        {
            var result = new Dictionary<string, string>();
            object paramColl = Activator.CreateInstance(_paramCollType);
            _addParam.Invoke(paramColl, new object[] { "TIPO", tipo });

            using (var reader = _ctor.Invoke(new[] { "SELECT IDENTIFICADOR, HASH FROM W_INDICEARTEFATOS WHERE TIPO = :TIPO", paramColl }) as IDisposable)
            {
                foreach (var row in (IEnumerable)reader)
                {
                    if (row == null) continue;
                    var indexer = row.GetType().GetProperties()
                        .First(p => p.GetIndexParameters().Length == 1 && p.GetIndexParameters()[0].ParameterType == typeof(int));
                    string id = GetString(indexer.GetValue(row, new object[] { 0 }));
                    string hash = GetString(indexer.GetValue(row, new object[] { 1 }));
                    if (!string.IsNullOrEmpty(id)) result[id] = hash ?? string.Empty;
                }
            }
            return result;
        }

        private static string GetString(object cell)
        {
            var m = cell?.GetType().GetMethod("GetString", Type.EmptyTypes);
            return m?.Invoke(cell, null)?.ToString();
        }
    }
}
