using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Myrtille.Web
{
    public static class PluginManager
    {
        /// <summary>
        /// Default directory for plugin assemblies.
        /// </summary>
        private const string PLUGIN_DIR = "plugins";

        /// <summary>
        /// Collection of loaded plugins.
        /// </summary>
        private static ICollection<object> Plugins = new List<object>();

        /// <summary>
        /// Loads and creates the instance of plugin assemblies from the directory with given type.
        /// </summary>
        /// <typeparam name="T">Type of plugins we want to be loaded</typeparam>
        public static void LoadPlugins<T>()
        {
            //
            // Gets the dll assemblies from the plugin directory
            string[] dllFileNames = null;
            if (Directory.Exists(AppDomain.CurrentDomain.RelativeSearchPath + "\\" + PLUGIN_DIR))
            {
                dllFileNames = Directory.GetFiles(AppDomain.CurrentDomain.RelativeSearchPath + "\\" + PLUGIN_DIR, "*.dll");
            }
            else
            {
                // No plugin directory found!
                return;
            }

            //
            // Loads the assemblies
            ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Length);
            foreach (string dllFile in dllFileNames)
            {
                var an = AssemblyName.GetAssemblyName(dllFile);
                var assembly = Assembly.Load(an);
                assemblies.Add(assembly);
            }

            //
            // Checks if the loaded assembly types are the ones needed
            Type pluginType = typeof(T);
            ICollection<Type> pluginTypes = new List<Type>();

            foreach (Assembly assembly in assemblies)
            {
                if (assembly == null) continue;

                Type[] types = assembly.GetTypes();
                foreach (Type type in types)
                {
                    if (type.IsInterface || type.IsAbstract) continue;

                    if (type.GetInterface(pluginType.FullName) != null)
                    {
                        pluginTypes.Add(type);
                    }
                }
            }

            //
            // Creates the instance of the correct assembly types
            foreach (Type type in pluginTypes)
            {
                var plugin = (T)Activator.CreateInstance(type);

                // Adds the created assemblies to the collection 
                Plugins.Add(plugin);
            }
        }

        /// <summary>
        /// Returns the plugins that are loaded and activated.
        /// </summary>
        /// <typeparam name="T">Type of plugins we want to retrieve</typeparam>
        /// <returns>ICollection of loaded and activated plugin assemblies</returns>
        public static IList<T> GetPlugins<T>()
        {
            return Plugins.OfType<T>().ToList();
        }
    }
}