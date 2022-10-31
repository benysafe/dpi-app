using InterfaceLibraryConfigurator;
using InterfaceLibraryDeserializer;
using InterfaceLibraryProcessor;
using InterfaceLibraryPublisher;
using InterfaceLibrarySerializer;
using InterfaceLibrarySubscriber;
using InterfaceLibraryLogger;
using Definitions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using NLog;
using Newtonsoft.Json;
using System.Threading;

namespace App
{
    class Program
    {
        public static IConfigurator _configurator;
        public static IGenericLogger _genericLogger;
        public static Logger _logger;
        public static IProcessor _processor;
        public static List<ISubscriber> _Subcribers = new List<ISubscriber>();
        public static Dictionary<string, string> _parameters = new Dictionary<string, string>();

        //nombre del módulo 
        static string _moduleName;
        //id del modulo
        static string _moduleId;

        //diccionario que contendra la configuracion referente al modulo
        public static Definitions.ModuleMakerConfig _moduleConfig;

        static void Main(string[] args)
        {
            try
            {
                new Thread(TerminateHandler).Start();
                

                if (args.Length >= 1)
                {
                    _moduleConfig = (ModuleMakerConfig)GetModuleConfiguration(args[0]);

                    if (args.Length >= 2)
                    {
                        for (int i = 1; i < args.Length + 1; i++)
                        {
                            string key;
                            string value;
                            if (getParameter(args[i], out key, out value))
                            {
                                _parameters.Add(key, value);
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception($"Faltan parametros en la llamada de ejecucion del modulo");
                }

                string libraryPath = null;
                string configPath = null;

                #region Modulo
                _moduleName = _moduleConfig.module.name;
                _moduleId = _moduleConfig.module.id;
                #endregion Modulo

                #region Logger
                if (_moduleConfig.genericLogger.libraryPath != "")
                    libraryPath = _moduleConfig.genericLogger.libraryPath;
                else
                    throw new Exception("Parametro erroneo");
                if (_moduleConfig.genericLogger.pathConfig != "")
                    configPath = _moduleConfig.genericLogger.pathConfig;
                else
                    throw new Exception("Parametro erroneo");

                _genericLogger = (IGenericLogger)Assembly_Load_metho<IGenericLogger>(libraryPath);
                _genericLogger.loadConfig(configPath);
                _logger = (Logger)_genericLogger.init(_moduleName);
                #endregion Logger

                #region Configuration
                if (_moduleConfig.configurator.libraryPath != "")
                    libraryPath = _moduleConfig.configurator.libraryPath;
                else
                    throw new Exception("Parametro erroneo");
                if (_moduleConfig.configurator.pathConfig != "")
                    configPath = _moduleConfig.configurator.pathConfig;
                else
                    throw new Exception("Parametro erroneo");

                _configurator = (IConfigurator)Assembly_Load_metho<IConfigurator>(libraryPath);
                _configurator.init(_genericLogger);
                _configurator.load(configPath);

                foreach (KeyValuePair<string, string> entry in _parameters)
                {
                    _configurator.addParameter(entry.Key, entry.Value);
                }
                #endregion Configuration

                #region Suscriptions
                for (int i = 0; i < _moduleConfig.subscriptions.ToList().Count; i++)
                //_moduleConfig.subscriptions.ToList().ForEach(subscriptor =>
                {
                    var subscriptor = _moduleConfig.subscriptions.ToList()[i];
                    libraryPath = subscriptor.libraryPath;
                    string suscriberId = subscriptor.id;
                    string suscriberName = subscriptor.name;
                    string suscriberFunction = subscriptor.function;
                    ISubscriber suscriber = (ISubscriber)Assembly_Load_metho<ISubscriber>(libraryPath);

                    #region Deserializer
                    string deserializerId = subscriptor.deserializer.id;
                    string deserializerName = subscriptor.deserializer.name;
                    libraryPath = subscriptor.deserializer.libraryPath;
                    IDeserializer deserializer = (IDeserializer)Assembly_Load_metho<IDeserializer>(libraryPath);

                    #region Processor
                    string processorName = subscriptor.deserializer.processor.name;
                    string processorId = subscriptor.deserializer.processor.id;
                    libraryPath = subscriptor.deserializer.processor.libraryPath;
                    _processor = (IProcessor)Assembly_Load_metho<IProcessor>(libraryPath);

                    #region Serializer
                    //para modulos que no publican datos
                    if (subscriptor.deserializer.processor.serializers.Length == 0)
                    {
                        _logger.Debug($"El parametro '{Definitions.App.Serializers}' no tiene asignado ningun valor");
                        _processor.init(_configurator, _genericLogger, processorId);
                    }
                    //para modulos que publican datos
                    else
                    {
                        for (int j = 0; j < subscriptor.deserializer.processor.serializers.ToList().Count; j++)
                        //subscriptor.deserializer.processor.serializers.ToList().ForEach(serialize =>
                        {
                            var serialize = subscriptor.deserializer.processor.serializers.ToList()[j];

                            libraryPath = serialize.libraryPath;
                            string serializerId = serialize.id;
                            string serializerName = serialize.name;
                            ISerializer serializer = (ISerializer)Assembly_Load_metho<ISerializer>(libraryPath);

                            #region Publisher
                            libraryPath = serialize.publishers.libraryPath;
                            string publisherId = serialize.publishers.id;
                            string publisherName = serialize.publishers.name;
                            IPublisher publisher = (IPublisher)Assembly_Load_metho<IPublisher>(libraryPath);
                            publisher.init(publisherId, _configurator, _genericLogger);
                            #endregion Publisher

                            serializer.init(serializerId, _configurator, publisher, _genericLogger);
                            #endregion Serializer

                            _processor.init(_configurator, _genericLogger, processorId);

                            //cada serializer tiene asignado un unico publisher, 
                            _processor.addSerializer(serializerName, serializer);
                            #endregion Processor
                        }//);
                    }

                    deserializer.init(deserializerId, _genericLogger);
                    deserializer.addProcessor(processorName, _processor);
                    #endregion Deserializer

                    suscriber.init(suscriberId, _configurator, _genericLogger);
                    suscriber.subscribe(suscriberFunction, deserializer);

                    _Subcribers.Add(suscriber);
                }//);
                #endregion Suscriptions

                #region Loop
                foreach (ISubscriber sus in _Subcribers)
                {
                    sus.startLoop();
                }
                #endregion Loop

            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
            }
        }

        public static Dictionary<string, TValue> ToDictionary<TValue>(object obj)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            var dictionary = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, TValue>>(json); ;
            return dictionary;
        }

        public static object GetModuleConfiguration(string path)
        {
            string strJson = System.IO.File.ReadAllText(path);
            var allConfig = JsonConvert.DeserializeObject<ModuleMakerConfig>(strJson);
            return allConfig as object;
        }

        public static T Assembly_Load_metho<T>(string path)
        {
            Assembly miExtensionAssembly = Assembly.LoadFile(path);
            List<Type> types = miExtensionAssembly.GetExportedTypes().ToList();

            if (!types.Any(eleTypes => eleTypes.GetTypeInfo().GetInterfaces().ToList().Any(eleInter => eleInter.FullName.Equals(typeof(T).FullName))))
            {
                throw new Exception($"no se encontro implementacion de la interfas '{typeof(T).FullName}', en el ensamblado {path}");
            }

            string typeFullName = types.Find(eleTypes => eleTypes.GetTypeInfo().GetInterfaces().ToList().Any(eleInter => eleInter.FullName.Equals(typeof(T).FullName))).FullName;

            Type miExtensionType = miExtensionAssembly.GetType(typeFullName);
            object miExtensionObjeto = Activator.CreateInstance(miExtensionType);

            return (T)miExtensionObjeto;
        }

        public static bool getParameter(string arg, out string key, out string value)
        {
            string[] strings = arg.Split('=', 2);
            if (strings.Length == 2)
            {
                key = strings[0];
                value = strings[1];
                return true;
            }
            else
            {
                throw new Exception($"El arguneto '{arg}' no esta definido correctamente");
            }
        }
        
        static void TerminateHandler()
        {
            foreach (ISubscriber sus in _Subcribers)
            {
                sus.endLoop();
            }
            _logger.Debug("Cierre suave del modulo");
            System.Environment.Exit(0);
        }
    }
}
