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
using System.Runtime.InteropServices;
using ModuleMakerConfig;
using DinamicLoad;

namespace App
{
    class Program
    {
        public static IConfigurator _configurator;
        public static IGenericLogger _genericLogger;
        public static Logger _logger;
        public static IProcessor _processor;
        public static List<ISubscriber> _Subcribers = new List<ISubscriber>();
        private static bool hasSuscriptor;


        public static Dictionary<string, string> _parameters = new Dictionary<string, string>();

        public static Dictionary<string, ISubscriber> _dirSubcriber = new Dictionary<string, ISubscriber>();
        public static Dictionary<string, IDeserializer> _dirDeserializer = new Dictionary<string, IDeserializer>();
        public static Dictionary<string, ISerializer> _dirSerializer = new Dictionary<string, ISerializer>();
        public static Dictionary<string, IPublisher> _dirPublisher = new Dictionary<string, IPublisher>();

        //nombre del módulo 
        static string _moduleName;
        //id del modulo
        static string _moduleId;

        //diccionario que contendra la configuracion referente al modulo
        public static ModuleMakerConfig.ModuleMakerConfig _moduleConfig;

        static void Main(string[] args)
        {
            try
            {
                if (args.Length >= 1)
                {
                    _moduleConfig = (ModuleMakerConfig.ModuleMakerConfig)GetModuleConfiguration(args[0]);

                    if (args.Length >= 2)
                    {
                        for (int i = 1; i < args.Length; i++)
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
                {
                    libraryPath = _moduleConfig.genericLogger.libraryPath;

                }
                else
                    throw new Exception("Parametro 'libraryPath' en 'genericLogger' erroneo");
                if (_moduleConfig.genericLogger.pathConfig != "")
                {
                    configPath = _moduleConfig.genericLogger.pathConfig;

                }
                else
                    throw new Exception("Parametro 'pathConfig' en 'genericLogger' erroneo");
                _genericLogger = (IGenericLogger)DinamicLoad.DinamicLoad.Assembly_Load_method<IGenericLogger>(libraryPath);

                _genericLogger.loadConfig(configPath);

                _logger = (Logger)_genericLogger.init(_moduleName);


                #endregion Logger
                _logger.Trace("Fin region 'Logger'");

                #region Configuration
                if (_moduleConfig.configurator.libraryPath != "")
                    libraryPath = _moduleConfig.configurator.libraryPath;
                else
                    throw new Exception("Parametro 'libraryPath' en 'configurator' erroneo");
                if (_moduleConfig.configurator.pathConfig != "")
                    configPath = _moduleConfig.configurator.pathConfig;
                else
                    throw new Exception("Parametro 'pathConfig' en 'configurator' erroneo");

                _configurator = (IConfigurator)DinamicLoad.DinamicLoad.Assembly_Load_method<IConfigurator>(libraryPath);
                _configurator.init(_genericLogger);
                _configurator.load(configPath);

                foreach (KeyValuePair<string, string> entry in _parameters)
                {
                    _configurator.addParameter(entry.Key, entry.Value);
                }

                #endregion Configuration
                _logger.Trace("Fin region 'Configuration'");

                #region Process
                string processorName;
                string processorId;
                if (_moduleConfig.processor.libraryPath != "")
                    libraryPath = _moduleConfig.processor.libraryPath;
                else
                    throw new Exception("Parametro 'libraryPath' en 'processor' erroneo");
                if (_moduleConfig.processor.name != "")
                    processorName = _moduleConfig.processor.name;
                else
                    throw new Exception("Parametro 'name' en 'processor' erroneo");
                if (_moduleConfig.processor.id != "")
                    processorId = _moduleConfig.processor.id;
                else
                    throw new Exception("Parametro 'id' en 'processor' erroneo");
                _processor = (IProcessor)DinamicLoad.DinamicLoad.Assembly_Load_method<IProcessor>(libraryPath);

                #endregion Process
                _logger.Trace("Fin region 'Process'");

                #region Subscriptors
                hasSuscriptor = true; 
                if (_moduleConfig.subscriptors == null || _moduleConfig.subscriptors.Length < 1)
                {
                    _logger.Debug("No se definio ningun susbcriptor");
                    hasSuscriptor = false;
                }
                if (hasSuscriptor)
                {
                    for (int iSubscriptors = 0; iSubscriptors < _moduleConfig.subscriptors.Length; iSubscriptors++)
                    {
                        var subscriptor = _moduleConfig.subscriptors.ToList()[iSubscriptors];
                        libraryPath = subscriptor.libraryPath;
                        string suscriberId = subscriptor.id;
                        string suscriberName = subscriptor.name;
                        ISubscriber suscriber = (ISubscriber)DinamicLoad.DinamicLoad.Assembly_Load_method<ISubscriber>(libraryPath);
                        _dirSubcriber.Add(suscriberId, suscriber);
                    }
                }
                #endregion Subscriptors
                _logger.Trace("Fin region 'Subcriptors'");

                #region Deserializers
                if (hasSuscriptor)
                {
                    if (_moduleConfig.deserializers.ToList().Count < 1)
                        throw new Exception("No se definio ningun deserializador");
                    for (int iDeserializer = 0; iDeserializer < _moduleConfig.deserializers.ToList().Count; iDeserializer++)
                    {
                        var deseializerConfig = _moduleConfig.deserializers.ToList()[iDeserializer];
                        libraryPath = deseializerConfig.libraryPath;
                        string deseializerId = deseializerConfig.id;
                        string deseializerName = deseializerConfig.name;
                        IDeserializer deserializer = (IDeserializer)DinamicLoad.DinamicLoad.Assembly_Load_method<IDeserializer>(libraryPath);
                        _dirDeserializer.Add(deseializerId, deserializer);
                    }
                }
                #endregion Deserializers
                _logger.Trace("Fin region 'Deserializers'");

                #region Serializers
                bool hasPublish = false;
                if (_moduleConfig.serializers is not null && _moduleConfig.serializers.ToList().Count > 0)
                {
                    hasPublish = true;
                    for (int iSerializer = 0; iSerializer < _moduleConfig.serializers.ToList().Count; iSerializer++)
                    {
                        var serializerConfig = _moduleConfig.serializers.ToList()[iSerializer];
                        libraryPath = serializerConfig.libraryPath;
                        string serializerId = serializerConfig.id;
                        string serializerName = serializerConfig.name;
                        ISerializer serializer = (ISerializer)DinamicLoad.DinamicLoad.Assembly_Load_method<ISerializer>(libraryPath);
                        _dirSerializer.Add(serializerId, serializer);
                    }
                    #endregion Serializers
                    _logger.Trace("Fin region 'Serializers'");

                    #region Publishers
                    if (_moduleConfig.publishers.ToList().Count < 0)
                        throw new Exception("No se definio ningun publicador");
                    for (int iPublisher = 0; iPublisher < _moduleConfig.publishers.ToList().Count; iPublisher++)
                    {
                        var publisherConfig = _moduleConfig.publishers.ToList()[iPublisher];
                        libraryPath = publisherConfig.libraryPath;
                        string publisherId = publisherConfig.id;
                        string publisherName = publisherConfig.name;
                        IPublisher publisher = (IPublisher)DinamicLoad.DinamicLoad.Assembly_Load_method<IPublisher>(libraryPath);
                        _dirPublisher.Add(publisherId, publisher);
                    }
                    #endregion Publishers
                    _logger.Trace("Fin region 'Publishers'");

                }
                _processor.init(_configurator, _genericLogger, processorId);

                #region SerializersTrees
                if (hasPublish)
                {
                    if (_moduleConfig.serializers_trees.ToList().Count < 1)
                        throw new Exception("No esta definido ningun arbor de correspondencias de serializacion");
                    var serialisersTrees = _moduleConfig.serializers_trees.ToList();
                    for (int indexS = 0; indexS < serialisersTrees.Count; indexS++)
                    {
                        string serializerId = serialisersTrees[indexS].id;
                        if (serialisersTrees[indexS].publishers_ids.ToList().Count < 1)
                            throw new Exception($"No esta definido ningun publicador para el serilizador '{serializerId}'");
                        List<string> publishersIds = serialisersTrees[indexS].publishers_ids.ToList();
                        ISerializer tempSerializer;
                        if (!_dirSerializer.TryGetValue(serializerId, out tempSerializer))
                            throw new Exception($"No se encontro el serializador '{serializerId}' en los serializadores instanciados");
                        tempSerializer.init(serializerId, _configurator, _genericLogger);
                        for (int indexP = 0; indexP < publishersIds.Count; indexP++)
                        {
                            IPublisher tempPublisher;
                            if (!_dirPublisher.TryGetValue(publishersIds[indexP], out tempPublisher))
                                throw new Exception($"No se encontro el publicador '{publishersIds[indexP]}' en los publicadores instanciados");
                            tempPublisher.init(publishersIds[indexP], _configurator, _genericLogger);
                            tempSerializer.addPublisher(publishersIds[indexP], tempPublisher);
                        }
                        _processor.addSerializer(tempSerializer);
                    }
                }
                #endregion SerializersTrees
                _logger.Trace("Fin region 'SerializersTrees'");

                #region SubscriptorsTrees
                if (hasSuscriptor)
                {
                    if (_moduleConfig.subscriptors_trees.Length < 1)
                        throw new Exception("No esta definido ningun arbor de correspondencias de subcripciones");
                    var subscriptorTrees = _moduleConfig.subscriptors_trees.ToList();
                    for (int indexS = 0; indexS < subscriptorTrees.Count; indexS++)
                    {
                        string subscriptorId = subscriptorTrees[indexS].id;
                        if (subscriptorTrees[indexS].deserializers_ids.ToList().Count < 1)
                            throw new Exception($"No esta definido ningun deserializador para el subcriptor '{subscriptorId}'");
                        List<string> deserializerIds = subscriptorTrees[indexS].deserializers_ids.ToList();
                        ISubscriber tempSubcriptor;
                        if (!_dirSubcriber.TryGetValue(subscriptorId, out tempSubcriptor))
                            throw new Exception($"No se encontro el subcriptor '{subscriptorId}' en los subcriptores instanciados");
                        tempSubcriptor.init(subscriptorId, _configurator, _genericLogger);
                        for (int indexP = 0; indexP < deserializerIds.Count; indexP++)
                        {
                            IDeserializer tempDeserializer;
                            if (!_dirDeserializer.TryGetValue(deserializerIds[indexP], out tempDeserializer))
                                throw new Exception($"No se encontro el deserializador '{deserializerIds[indexP]}' en los deserializadores instanciados");
                            tempDeserializer.init(deserializerIds[indexP], _configurator, _genericLogger);
                            tempDeserializer.addProcessor(_processor);
                            tempSubcriptor.addDeserializer(deserializerIds[indexP], tempDeserializer);
                        }
                        if (_parameters.Keys.Contains("subId"))
                        {
                            tempSubcriptor.subscribe(_parameters["subId"]);
                        }
                        else
                        {
                            tempSubcriptor.subscribe(null);
                        }
                        _Subcribers.Add(tempSubcriptor);
                    }
                }
                #endregion  SubscriptorsTrees
                _logger.Trace("Fin region 'SubscriptorTrees'");

                Action<PosixSignalContext> handler = TerminateHandler;
                PosixSignalRegistration.Create(PosixSignal.SIGTERM, handler);

                #region Loop
                if (hasSuscriptor)
                {
                    foreach (ISubscriber sus in _Subcribers)
                    {
                        Thread threadSubcriptor = new Thread(sus.startLoop);
                        threadSubcriptor.IsBackground = false;
                        threadSubcriptor.Start();
                    }
                }
                else
                {
                    Thread threadSubcriptor = new Thread(loopWithOutSuscriptor);
                    threadSubcriptor.IsBackground = false;
                    threadSubcriptor.Start();
                }
                #endregion Loop


            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());

                throw ex;
            }
        }

        private static void loopWithOutSuscriptor()
        {
            while (true)
            {
                Task.Delay(50).Wait();
            }
        }

        public static object GetModuleConfiguration(string path)
        {
            try
            {
                string strJson = System.IO.File.ReadAllText(path);

                var allConfig = JsonConvert.DeserializeObject<ModuleMakerConfig.ModuleMakerConfig>(strJson);

                return allConfig as object;
            }
            catch(Exception e)
            {
                _logger.Error(e.ToString());
                return null;
            }
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
                throw new Exception($"El argumeto '{arg}' no esta definido correctamente");
            }
        }

        static void TerminateHandler(PosixSignalContext context)
        {
            context.Cancel = true;
            if (hasSuscriptor)
            {
                foreach (ISubscriber sus in _Subcribers)
                {
                    sus.endLoop();
                }
            }
            _logger.Debug("Cierre suave del modulo");
            Environment.Exit(0);
        }
    }
}
