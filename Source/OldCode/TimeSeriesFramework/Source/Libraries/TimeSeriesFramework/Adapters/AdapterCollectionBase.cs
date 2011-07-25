﻿//******************************************************************************************************
//  AdapterCollectionBase.cs - Gbtc
//
//  Copyright © 2010, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  09/02/2010 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using TVA;
using TVA.IO;
using TVA.Units;

namespace TimeSeriesFramework.Adapters
{
    /// <summary>
    /// Represents a collection of <see cref="IAdapter"/> implementations.
    /// </summary>
    /// <typeparam name="T">Type of <see cref="IAdapter"/> this collection contains.</typeparam>
    public abstract class AdapterCollectionBase<T> : Collection<T>, IAdapterCollection where T : IAdapter
    {
        #region [ Members ]

        // Events

        /// <summary>
        /// Provides status messages to consumer.
        /// </summary>
        /// <remarks>
        /// <see cref="EventArgs{T}.Argument"/> is new status message.
        /// </remarks>
        public event EventHandler<EventArgs<string>> StatusMessage;

        /// <summary>
        /// Event is raised when there is an exception encountered while processing.
        /// </summary>
        /// <remarks>
        /// <see cref="EventArgs{T}.Argument"/> is the exception that was thrown.
        /// </remarks>
        public event EventHandler<EventArgs<Exception>> ProcessException;

        /// <summary>
        /// Event is raised when <see cref="InputMeasurementKeys"/> are updated.
        /// </summary>
        public event EventHandler InputMeasurementKeysUpdated;

        /// <summary>
        /// Event is raised when <see cref="OutputMeasurements"/> are updated.
        /// </summary>
        public event EventHandler OutputMeasurementsUpdated;

        /// <summary>
        /// Event is raised when this <see cref="AdapterCollectionBase{T}"/> is disposed or an <see cref="IAdapter"/> in the collection is disposed.
        /// </summary>
        public event EventHandler Disposed;

        // Fields
        private string m_name;
        private uint m_id;
        private bool m_initialized;
        private string m_connectionString;
        private IAdapterCollection m_parent;
        private Dictionary<string, string> m_settings;
        private DataSet m_dataSource;
        private string m_dataMember;
        private int m_initializationTimeout;
        private bool m_autoStart;
        private bool m_processMeasurementFilter;
        private IMeasurement[] m_outputMeasurements;
        private MeasurementKey[] m_inputMeasurementKeys;
        private string[] m_inputSourceIDs;
        private string[] m_outputSourceIDs;
        private MeasurementKey[] m_requestedInputMeasurementKeys;
        private MeasurementKey[] m_requestedOutputMeasurementKeys;
        private Ticks m_lastProcessTime;
        private Time m_totalProcessTime;
        private long m_processedMeasurements;
        private System.Timers.Timer m_monitorTimer;
        private bool m_monitorTimerEnabled;
        private bool m_enabled;
        private bool m_disposed;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Constructs a new instance of the <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        protected AdapterCollectionBase()
        {
            m_name = this.GetType().Name;
            m_settings = new Dictionary<string, string>();
            m_initializationTimeout = AdapterBase.DefaultInitializationTimeout;
            m_autoStart = true;

            m_monitorTimer = new System.Timers.Timer();
            m_monitorTimer.Elapsed += m_monitorTimer_Elapsed;

            // We monitor total number of processed measurements every minute
            m_monitorTimer.Interval = 60000;
            m_monitorTimer.AutoReset = true;
            m_monitorTimer.Enabled = false;
        }

        /// <summary>
        /// Releases the unmanaged resources before the <see cref="AdapterCollectionBase{T}"/> object is reclaimed by <see cref="GC"/>.
        /// </summary>
        ~AdapterCollectionBase()
        {
            Dispose(false);
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets the name of this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        public virtual string Name
        {
            get
            {
                return m_name;
            }
            set
            {
                m_name = value;
            }
        }

        /// <summary>
        /// Gets or sets numeric ID associated with this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        public virtual uint ID
        {
            get
            {
                return m_id;
            }
            set
            {
                m_id = value;
            }
        }

        /// <summary>
        /// Gets or sets flag indicating if the adapter collection has been initialized successfully.
        /// </summary>
        public virtual bool Initialized
        {
            get
            {
                return m_initialized;
            }
            set
            {
                m_initialized = value;
            }
        }

        /// <summary>
        /// Gets or sets key/value pair connection information specific to this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        public virtual string ConnectionString
        {
            get
            {
                return m_connectionString;
            }
            set
            {
                m_connectionString = value;

                // Preparse settings upon connection string assignment
                if (string.IsNullOrWhiteSpace(m_connectionString))
                    m_settings = new Dictionary<string, string>();
                else
                    m_settings = m_connectionString.ParseKeyValuePairs();
            }
        }

        /// <summary>
        /// Gets a read-only reference to the collection that contains this <see cref="AdapterCollectionBase{T}"/>, if any.
        /// </summary>
        public ReadOnlyCollection<IAdapter> Parent
        {
            get
            {
                return new ReadOnlyCollection<IAdapter>(m_parent);
            }
        }

        /// <summary>
        /// Gets or sets <see cref="DataSet"/> based data source used to load each <see cref="IAdapter"/>.
        /// Updates to this property will cascade to all items in this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        /// <remarks>
        /// Table name specified in <see cref="DataMember"/> from <see cref="DataSource"/> is expected
        /// to have the following table column names:<br/>
        /// ID, AdapterName, AssemblyName, TypeName, ConnectionString<br/>
        /// ID column type should be integer based, all other column types are expected to be string based.
        /// </remarks>
        public virtual DataSet DataSource
        {
            get
            {
                return m_dataSource;
            }
            set
            {
                m_dataSource = value;

                // Update data source for items in this collection
                lock (this)
                {
                    foreach (T item in this)
                    {
                        item.DataSource = m_dataSource;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets specific data member (e.g., table name) in <see cref="DataSource"/> used to <see cref="Initialize"/> this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        /// <remarks>
        /// Table name specified in <see cref="DataMember"/> from <see cref="DataSource"/> is expected
        /// to have the following table column names:<br/>
        /// ID, AdapterName, AssemblyName, TypeName, ConnectionString<br/>
        /// ID column type should be integer based, all other column types are expected to be string based.
        /// </remarks>
        public virtual string DataMember
        {
            get
            {
                return m_dataMember;
            }
            set
            {
                m_dataMember = value;
            }
        }

        /// <summary>
        /// Gets or sets the default adapter time that represents the maximum time system will wait during <see cref="Start"/> for initialization.
        /// </summary>
        /// <remarks>
        /// Set to <see cref="Timeout.Infinite"/> to wait indefinitely.
        /// </remarks>
        public virtual int InitializationTimeout
        {
            get
            {
                return m_initializationTimeout;
            }
            set
            {
                m_initializationTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets flag indicating if adapter collection should automatically start items when <see cref="AutoInitialize"/> is <c>false</c>.
        /// </summary>
        public virtual bool AutoStart
        {
            get
            {
                return m_autoStart;
            }
            set
            {
                m_autoStart = value;
            }
        }

        /// <summary>
        /// Gets or sets flag that determines if measurements being queued for processing should be tested to see if they are in the <see cref="InputMeasurementKeys"/>.
        /// </summary>
        public virtual bool ProcessMeasurementFilter
        {
            get
            {
                return m_processMeasurementFilter;
            }
            set
            {
                m_processMeasurementFilter = value;

                // Update this flag for items in this collection
                lock (this)
                {
                    foreach (T item in this)
                    {
                        item.ProcessMeasurementFilter = m_processMeasurementFilter;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets primary keys of input measurements the <see cref="AdapterCollectionBase{T}"/> expects, if any.
        /// </summary>
        public virtual MeasurementKey[] InputMeasurementKeys
        {
            get
            {
                // If a specific set of input measurement keys has been assigned, use that set
                if (m_inputMeasurementKeys != null)
                    return m_inputMeasurementKeys;

                // Otherwise return cumulative results of all child adapters
                lock (this)
                {
                    // If any of the children expects all measurements (i.e., null InputMeasurementKeys) then the parent collection must expect all measurements
                    if (this.Any<IAdapter>(item => item.InputMeasurementKeys == null))
                        return null;

                    return this.SelectMany<IAdapter, MeasurementKey>(item => item.InputMeasurementKeys).Distinct().ToArray();
                }
            }
            set
            {
                m_inputMeasurementKeys = value;
                OnInputMeasurementKeysUpdated();
            }
        }

        /// <summary>
        /// Gets or sets output measurements that the <see cref="AdapterCollectionBase{T}"/> will produce, if any.
        /// </summary>
        public virtual IMeasurement[] OutputMeasurements
        {
            get
            {
                // If a specific set of output measurements has been assigned, use that set
                if (m_outputMeasurements != null)
                    return m_outputMeasurements;

                // Otherwise return cumulative results of all child adapters
                lock (this)
                {
                    return this.Where<IAdapter>(item => item.OutputMeasurements != null).SelectMany<IAdapter, IMeasurement>(item => item.OutputMeasurements).Distinct().ToArray();
                }
            }
            set
            {
                m_outputMeasurements = value;
                OnOutputMeasurementsUpdated();
            }
        }

        /// <summary>
        /// Gets or sets <see cref="MeasurementKey.Source"/> values used to filter input measurement keys.
        /// </summary>
        /// <remarks>
        /// The collection classes simply track this value if assigned, no automatic action is taken.
        /// </remarks>
        public virtual string[] InputSourceIDs
        {
            get
            {
                return m_inputSourceIDs;
            }
            set
            {
                m_inputSourceIDs = value;
            }
        }

        /// <summary>
        /// Gets or sets <see cref="MeasurementKey.Source"/> values used to filter output measurements.
        /// </summary>
        /// <remarks>
        /// The collection classes simply track this value if assigned, no automatic action is taken.
        /// </remarks>
        public virtual string[] OutputSourceIDs
        {
            get
            {
                return m_outputSourceIDs;
            }
            set
            {
                m_outputSourceIDs = value;
            }
        }

        /// <summary>
        /// Gets or sets input measurement keys that are requested by other adapters based on what adapter says it can provide.
        /// </summary>
        public virtual MeasurementKey[] RequestedInputMeasurementKeys
        {
            get
            {
                // If a specific set of input measurement keys has been assigned, use that set
                if (m_requestedInputMeasurementKeys != null)
                    return m_requestedInputMeasurementKeys;

                // Otherwise return cumulative results of all child adapters
                lock (this)
                {
                    if (typeof(T) is IActionAdapter)
                        return this.Cast<IActionAdapter>().Where(item => item.RequestedInputMeasurementKeys != null).SelectMany(item => item.RequestedInputMeasurementKeys).Distinct().ToArray();
                    else if (typeof(T) is IOutputAdapter)
                        return this.Cast<IOutputAdapter>().Where(item => item.RequestedInputMeasurementKeys != null).SelectMany(item => item.RequestedInputMeasurementKeys).Distinct().ToArray();
                }

                return null;
            }
            set
            {
                m_requestedInputMeasurementKeys = value;
            }
        }

        /// <summary>
        /// Gets or sets output measurement keys that are requested by other adapters based on what adapter says it can provide.
        /// </summary>
        public virtual MeasurementKey[] RequestedOutputMeasurementKeys
        {
            get
            {
                // If a specific set of output measurement keys has been assigned, use that set
                if (m_requestedOutputMeasurementKeys != null)
                    return m_requestedOutputMeasurementKeys;

                // Otherwise return cumulative results of all child adapters
                lock (this)
                {
                    if (typeof(T) is IActionAdapter)
                        return this.Cast<IActionAdapter>().Where(item => item.RequestedOutputMeasurementKeys != null).SelectMany(item => item.RequestedOutputMeasurementKeys).Distinct().ToArray();
                    else if (typeof(T) is IInputAdapter)
                        return this.Cast<IInputAdapter>().Where(item => item.RequestedOutputMeasurementKeys != null).SelectMany(item => item.RequestedOutputMeasurementKeys).Distinct().ToArray();
                }

                return null;
            }
            set
            {
                m_requestedOutputMeasurementKeys = value;
            }
        }

        /// <summary>
        /// Gets the total number of measurements processed thus far by each <see cref="IAdapter"/> implementation
        /// in the <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        public virtual long ProcessedMeasurements
        {
            get
            {
                long processedMeasurements = 0;

                // Calculate new total for all archive destined output adapters
                lock (this)
                {
                    foreach (IAdapter item in this)
                    {
                        processedMeasurements += item.ProcessedMeasurements;
                    }
                }

                return processedMeasurements;
            }
        }

        /// <summary>
        /// Gets or sets enabled state of this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        public virtual bool Enabled
        {
            get
            {
                return m_enabled;
            }
            set
            {
                if (m_enabled && !value)
                    Stop();
                else if (!m_enabled && value)
                    Start();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="AdapterCollectionBase{T}"/> is read-only.
        /// </summary>
        public virtual bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets or sets flag that determines if monitor timer should be used for monitoring processed measurement statistics for the <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        protected virtual bool MonitorTimerEnabled
        {
            get
            {
                return m_monitorTimerEnabled;
            }
            set
            {
                m_monitorTimerEnabled = value;

                if (m_monitorTimer != null)
                    m_monitorTimer.Enabled = value && Enabled;
            }
        }

        /// <summary>
        /// Gets flag that detemines if <see cref="IAdapter"/> implementations are automatically initialized
        /// when they are added to the collection.
        /// </summary>
        protected virtual bool AutoInitialize
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets settings <see cref="Dictionary{TKey,TValue}"/> parsed when <see cref="ConnectionString"/> was assigned.
        /// </summary>
        public Dictionary<string, string> Settings
        {
            get
            {
                return m_settings;
            }
        }

        /// <summary>
        /// Gets the descriptive status of this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        public virtual string Status
        {
            get
            {
                StringBuilder status = new StringBuilder();
                DataSet dataSource = this.DataSource;

                // Show collection status
                status.AppendFormat("  Total adapter components: {0}", Count);
                status.AppendLine();
                status.AppendFormat("    Collection initialized: {0}", Initialized);
                status.AppendLine();
                status.AppendFormat("         Parent collection: {0}", m_parent == null ? "Undefined" : m_parent.Name);
                status.AppendLine();
                status.AppendFormat("    Initialization timeout: {0}", InitializationTimeout < 0 ? "Infinite" : InitializationTimeout.ToString() + " milliseconds");
                status.AppendLine();
                status.AppendFormat(" Using measurement routing: {0}", !ProcessMeasurementFilter);
                status.AppendLine();
                status.AppendFormat(" Current operational state: {0}", (Enabled ? "Enabled" : "Disabled"));
                status.AppendLine();
                if (MonitorTimerEnabled)
                {
                    status.AppendFormat("    Processed measurements: {0}", m_processedMeasurements.ToString("N0"));
                    status.AppendLine();
                    status.AppendFormat("   Average processing rate: {0} measurements / second", ((int)(m_processedMeasurements / m_totalProcessTime)).ToString("N0"));
                    status.AppendLine();
                }
                status.AppendFormat("       Data source defined: {0}", (dataSource != null));
                status.AppendLine();
                if (dataSource != null)
                {
                    status.AppendFormat("    Referenced data source: {0}, {1} tables", dataSource.DataSetName, dataSource.Tables.Count);
                    status.AppendLine();
                }
                status.AppendFormat("    Data source table name: {0}", DataMember);
                status.AppendLine();

                if (Count > 0)
                {
                    int index = 0;

                    status.AppendLine();
                    status.AppendFormat("Status of each {0} component:", Name);
                    status.AppendLine();
                    status.Append(new string('-', 79));
                    status.AppendLine();

                    // Show the status of registered components.
                    lock (this)
                    {
                        foreach (T item in this)
                        {
                            IProvideStatus statusProvider = item as IProvideStatus;

                            if (statusProvider != null)
                            {
                                // This component provides status information.                       
                                status.AppendLine();
                                status.AppendFormat("Status of {0} component {1}, {2}:", typeof(T).Name, ++index, statusProvider.Name);
                                status.AppendLine();
                                try
                                {
                                    status.Append(statusProvider.Status);
                                }
                                catch (Exception ex)
                                {
                                    status.AppendFormat("Failed to retrieve status due to exception: {0}", ex.Message);
                                    status.AppendLine();
                                }
                            }
                        }
                    }

                    status.AppendLine();
                    status.Append(new string('-', 79));
                    status.AppendLine();
                }

                return status.ToString();
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Releases all the resources used by the <see cref="AdapterCollectionBase{T}"/> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AdapterCollectionBase{T}"/> object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                try
                {
                    if (disposing)
                    {
                        if (m_monitorTimer != null)
                        {
                            m_monitorTimer.Elapsed -= m_monitorTimer_Elapsed;
                            m_monitorTimer.Dispose();
                        }
                        m_monitorTimer = null;

                        Clear();        // This disposes all items in collection...
                    }
                }
                finally
                {
                    m_disposed = true;  // Prevent duplicate dispose.

                    if (Disposed != null)
                        Disposed(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Loads all <see cref="IAdapter"/> implementations defined in <see cref="DataSource"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Table name specified in <see cref="DataMember"/> from <see cref="DataSource"/> is expected
        /// to have the following table column names:<br/>
        /// ID, AdapterName, AssemblyName, TypeName, ConnectionString<br/>
        /// ID column type should be integer based, all other column types are expected to be string based.
        /// </para>
        /// <para>
        /// Note that when calling this method any existing items will be cleared allowing a "re-initialize".
        /// </para>
        /// </remarks>
        /// <exception cref="NullReferenceException">DataSource is null.</exception>
        /// <exception cref="InvalidOperationException">DataMember is null or empty.</exception>
        public virtual void Initialize()
        {
            if (DataSource == null)
                throw new NullReferenceException(string.Format("DataSource is null, cannot load {0}", Name));

            if (string.IsNullOrWhiteSpace(DataMember))
                throw new InvalidOperationException(string.Format("DataMember is null or empty, cannot load {0}", Name));

            Initialized = false;

            Dictionary<string, string> settings = Settings;
            string setting;
            T item;

            // Load the default initialization parameter for adapters in this collection
            if (settings.TryGetValue("initializationTimeout", out setting))
                InitializationTimeout = int.Parse(setting);

            lock (this)
            {
                Clear();

                if (DataSource.Tables.Contains(DataMember))
                {
                    foreach (DataRow adapterRow in DataSource.Tables[DataMember].Rows)
                    {
                        if (TryCreateAdapter(adapterRow, out item))
                            Add(item);
                    }

                    Initialized = true;
                }
                else
                    throw new InvalidOperationException(string.Format("Data set member \"{0}\" was not found in data source, check ConfigurationEntity. Failed to initialize {1}.", DataMember, Name));
            }
        }

        /// <summary>
        /// Attempts to create an <see cref="IAdapter"/> from the specified <see cref="DataRow"/>.
        /// </summary>
        /// <param name="adapterRow"><see cref="DataRow"/> containing item information to initialize.</param>
        /// <param name="adapter">Initialized adapter if successful; otherwise null.</param>
        /// <returns><c>true</c> if item was successfully initialized; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// See <see cref="DataSource"/> property for expected <see cref="DataRow"/> column names.
        /// </remarks>
        /// <exception cref="NullReferenceException"><paramref name="adapterRow"/> is null.</exception>
        public virtual bool TryCreateAdapter(DataRow adapterRow, out T adapter)
        {
            if (adapterRow == null)
                throw new NullReferenceException(string.Format("Cannot initialize from null adpater DataRow"));

            Assembly assembly;
            string name = "", assemblyName = "", typeName = "", connectionString, setting;
            uint id;

            try
            {
                name = adapterRow["AdapterName"].ToNonNullString("[IAdapter]");
                assemblyName = FilePath.GetAbsolutePath(adapterRow["AssemblyName"].ToNonNullString());
                typeName = adapterRow["TypeName"].ToNonNullString();
                connectionString = adapterRow["ConnectionString"].ToNonNullString();
                id = uint.Parse(adapterRow["ID"].ToNonNullString("0"));

                if (string.IsNullOrWhiteSpace(typeName))
                    throw new InvalidOperationException("No adapter type was defined");

                if (!File.Exists(assemblyName))
                    throw new InvalidOperationException("Specified adapter assembly does not exist");

                assembly = Assembly.LoadFrom(assemblyName);
                adapter = (T)Activator.CreateInstance(assembly.GetType(typeName));

                // Assign critical adapter properties
                adapter.Name = name;
                adapter.ID = id;
                adapter.ConnectionString = connectionString;
                adapter.DataSource = DataSource;

                // Assign adapter initialization timeout   
                if (adapter.Settings.TryGetValue("initializationTimeout", out setting))
                    adapter.InitializationTimeout = int.Parse(setting);
                else
                    adapter.InitializationTimeout = InitializationTimeout;

                return true;
            }
            catch (Exception ex)
            {
                // We report any errors encountered during type creation...
                OnProcessException(new InvalidOperationException(string.Format("Failed to load adapter \"{0}\" [{1}] from \"{2}\": {3}", name, typeName, assemblyName, ex.Message), ex));
            }

            adapter = default(T);
            return false;
        }

        // Explicit IAdapter implementation of TryCreateAdapter
        bool IAdapterCollection.TryCreateAdapter(DataRow adapterRow, out IAdapter adapter)
        {
            T adapterT;
            bool result = TryCreateAdapter(adapterRow, out adapterT);
            adapter = adapterT as IAdapter;
            return result;
        }

        /// <summary>
        /// Attempts to get the adapter with the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">ID of adapter to get.</param>
        /// <param name="adapter">Adapter reference if found; otherwise null.</param>
        /// <returns><c>true</c> if adapter with the specified <paramref name="id"/> was found; otherwise <c>false</c>.</returns>
        public virtual bool TryGetAdapterByID(uint id, out T adapter)
        {
            return TryGetAdapter<uint>(id, (item, value) => item.ID == value, out adapter);
        }

        /// <summary>
        /// Attempts to get the adapter with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">Name of adapter to get.</param>
        /// <param name="adapter">Adapter reference if found; otherwise null.</param>
        /// <returns><c>true</c> if adapter with the specified <paramref name="name"/> was found; otherwise <c>false</c>.</returns>
        public virtual bool TryGetAdapterByName(string name, out T adapter)
        {
            return TryGetAdapter<string>(name, (item, value) => string.Compare(item.Name, value, true) == 0, out adapter);
        }

        /// <summary>
        /// Attempts to get the adapter with the specified <paramref name="value"/> given <paramref name="testItem"/> function.
        /// </summary>
        /// <param name="value">Value of adapter to get.</param>
        /// <param name="testItem">Function delegate used to test item <paramref name="value"/>.</param>
        /// <param name="adapter">Adapter reference if found; otherwise null.</param>
        /// <returns><c>true</c> if adapter with the specified <paramref name="value"/> was found; otherwise <c>false</c>.</returns>
        protected virtual bool TryGetAdapter<TValue>(TValue value, Func<T, TValue, bool> testItem, out T adapter)
        {
            lock (this)
            {
                foreach (T item in this)
                {
                    if (testItem(item, value))
                    {
                        adapter = item;
                        return true;
                    }
                }
            }

            adapter = default(T);
            return false;
        }

        // Explicit IAdapter implementation of TryGetAdapterByID
        bool IAdapterCollection.TryGetAdapterByID(uint id, out IAdapter adapter)
        {
            T adapterT;
            bool result = TryGetAdapterByID(id, out adapterT);
            adapter = adapterT as IAdapter;
            return result;
        }

        // Explicit IAdapter implementation of TryGetAdapterByName
        bool IAdapterCollection.TryGetAdapterByName(string name, out IAdapter adapter)
        {
            T adapterT;
            bool result = TryGetAdapterByName(name, out adapterT);
            adapter = adapterT as IAdapter;
            return result;
        }

        /// <summary>
        /// Attempts to initialize (or reinitialize) an individual <see cref="IAdapter"/> based on its ID.
        /// </summary>
        /// <param name="id">The numeric ID associated with the <see cref="IAdapter"/> to be initialized.</param>
        /// <returns><c>true</c> if item was successfully initialized; otherwise <c>false</c>.</returns>
        public virtual bool TryInitializeAdapterByID(uint id)
        {
            T newAdapter, oldAdapter;
            uint rowID;

            foreach (DataRow adapterRow in DataSource.Tables[DataMember].Rows)
            {
                rowID = uint.Parse(adapterRow["ID"].ToNonNullString("0"));

                if (rowID == id)
                {
                    if (TryCreateAdapter(adapterRow, out newAdapter))
                    {
                        // Found and created new item - update collection reference
                        bool foundItem = false;

                        lock (this)
                        {
                            for (int i = 0; i < Count; i++)
                            {
                                oldAdapter = this[i];

                                if (oldAdapter.ID == id)
                                {
                                    // Stop old item
                                    oldAdapter.Stop();

                                    // Dispose old item, initialize new item
                                    this[i] = newAdapter;

                                    // Attempt to start new item
                                    if (AutoInitialize)
                                        ThreadPool.QueueUserWorkItem(StartItem, newAdapter);
                                    else if (AutoStart)
                                        newAdapter.Start();

                                    foundItem = true;
                                    break;
                                }
                            }

                            // Add item to collection if it didn't exist
                            if (!foundItem)
                            {
                                // Add new adapter to the collection
                                Add(newAdapter);

                                // Start new item
                                if (AutoInitialize)
                                    ThreadPool.QueueUserWorkItem(StartItem, newAdapter);
                                else if (AutoStart)
                                    newAdapter.Start();
                            }

                            return true;
                        }
                    }

                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Starts, or restarts, each <see cref="IAdapter"/> implementation in this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        [AdapterCommand("Starts, or restarts, each adapter in the collection.")]
        public virtual void Start()
        {
            // Make sure we are stopped (e.g., disconnected) before attempting to start (e.g., connect)
            if (m_enabled)
                Stop();

            m_enabled = true;

            ResetStatistics();

            lock (this)
            {
                foreach (T item in this)
                {
                    try
                    {
                        // We start items from thread pool if auto-intializing since
                        // start will block and wait for initialization to complete
                        if (AutoInitialize)
                            ThreadPool.QueueUserWorkItem(StartItem, item);
                        else if (AutoStart)
                            item.Start();
                    }
                    catch (Exception ex)
                    {
                        // We report any errors encountered during type creation...
                        OnProcessException(new InvalidOperationException(string.Format("Failed to start adapter: {0}", ex.Message), ex));
                    }
                }
            }

            // Start data monitor...
            if (MonitorTimerEnabled)
                m_monitorTimer.Start();
        }

        // Thread pool delegate to handle item startup
        private void StartItem(object state)
        {
            T item = (T)state;

            try
            {
                // Wait for adapter intialization to complete and see if item is set to auto-start
                if (item.WaitForInitialize(item.InitializationTimeout))
                {
                    if (item.AutoStart)
                        item.Start();
                }
                else
                {
                    OnProcessException(new TimeoutException("Timeout waiting for adapter initialization."));
                }
            }
            catch (Exception ex)
            {
                // We report any errors encountered during startup...
                OnProcessException(new InvalidOperationException(string.Format("Failed to start adapter: {0}", ex.Message), ex));
            }
        }

        /// <summary>
        /// Stops each <see cref="IAdapter"/> implementation in this <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        [AdapterCommand("Stops each adapter in the collection.")]
        public virtual void Stop()
        {
            m_enabled = false;

            lock (this)
            {
                foreach (T item in this)
                {
                    item.Stop();
                }
            }

            // Stop data monitor...
            m_monitorTimer.Stop();
        }

        // Assigns the reference to the parent adapter collection that will contain this adapter collection, if any.
        void IAdapter.AssignParentCollection(IAdapterCollection parent)
        {
            m_parent = parent;
        }

        /// <summary>
        /// Resets the statistics of this collection.
        /// </summary>
        [AdapterCommand("Resets the statistics of this collection.")]
        public void ResetStatistics()
        {
            m_processedMeasurements = 0;
            m_totalProcessTime = 0.0D;
            m_lastProcessTime = DateTime.UtcNow.Ticks;

            OnStatusMessage("Statistics reset for this collection.");
        }

        /// <summary>
        /// Gets a short one-line status of this <see cref="AdapterBase"/>.
        /// </summary>
        /// <param name="maxLength">Maximum number of available characters for display.</param>
        /// <returns>A short one-line summary of the current status of this <see cref="AdapterBase"/>.</returns>
        public virtual string GetShortStatus(int maxLength)
        {
            return string.Format("Total components: {0:N0}", Count).CenterText(maxLength);
        }

        /// <summary>
        /// This method does not wait for <see cref="AdapterCollectionBase{T}"/>.
        /// </summary>
        /// <param name="timeout">This parameter is ignored.</param>
        /// <returns><c>true</c> for <see cref="AdapterCollectionBase{T}"/>.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual bool WaitForInitialize(int timeout)
        {
            // Adapter collections have no need to wait
            return true;
        }

        /// <summary>
        /// Raises the <see cref="StatusMessage"/> event.
        /// </summary>
        /// <param name="status">New status message.</param>
        protected virtual void OnStatusMessage(string status)
        {
            if (StatusMessage != null)
                StatusMessage(this, new EventArgs<string>(status));
        }

        /// <summary>
        /// Raises the <see cref="StatusMessage"/> event with a formatted status message.
        /// </summary>
        /// <param name="formattedStatus">Formatted status message.</param>
        /// <param name="args">Arguments for <paramref name="formattedStatus"/>.</param>
        /// <remarks>
        /// This overload combines string.Format and SendStatusMessage for convienence.
        /// </remarks>
        internal protected virtual void OnStatusMessage(string formattedStatus, params object[] args)
        {
            if (StatusMessage != null)
                StatusMessage(this, new EventArgs<string>(string.Format(formattedStatus, args)));
        }

        /// <summary>
        /// Raises <see cref="ProcessException"/> event.
        /// </summary>
        /// <param name="ex">Processing <see cref="Exception"/>.</param>
        internal protected virtual void OnProcessException(Exception ex)
        {
            if (ProcessException != null)
                ProcessException(this, new EventArgs<Exception>(ex));
        }

        /// <summary>
        /// Raises <see cref="InputMeasurementKeysUpdated"/> event.
        /// </summary>
        protected virtual void OnInputMeasurementKeysUpdated()
        {
            if (InputMeasurementKeysUpdated != null)
                InputMeasurementKeysUpdated(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises <see cref="OutputMeasurementsUpdated"/> event.
        /// </summary>
        protected virtual void OnOutputMeasurementsUpdated()
        {
            if (OutputMeasurementsUpdated != null)
                OutputMeasurementsUpdated(this, EventArgs.Empty);
        }

        /// <summary>
        /// Removes all elements from the <see cref="Collection{T}"/>.
        /// </summary>
        protected override void ClearItems()
        {
            // Dispose each item before clearing the collection
            lock (this)
            {
                foreach (T item in this)
                {
                    DisposeItem(item);
                }

                base.ClearItems();
            }
        }

        /// <summary>
        /// Inserts an element into the <see cref="Collection{T}"/> the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="item">The <see cref="IAdapter"/> implementation to insert.</param>
        protected override void InsertItem(int index, T item)
        {
            lock (this)
            {
                // Wire up item events and handle item initialization
                InitializeItem(item);
                base.InsertItem(index, item);
            }
        }

        /// <summary>
        /// Assigns a new element to the <see cref="Collection{T}"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index for which item should be assigned.</param>
        /// <param name="item">The <see cref="IAdapter"/> implementation to assign.</param>
        protected override void SetItem(int index, T item)
        {
            lock (this)
            {
                // Dispose of existing item
                DisposeItem(this[index]);

                // Wire up item events and handle initialization of new item
                InitializeItem(item);

                base.SetItem(index, item);
            }
        }

        /// <summary>
        /// Removes the element at the specified index of the <see cref="Collection{T}"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        protected override void RemoveItem(int index)
        {
            // Dispose of item before removing it from the collection
            lock (this)
            {
                DisposeItem(this[index]);
                base.RemoveItem(index);
            }
        }

        /// <summary>
        /// Wires events and initializes new <see cref="IAdapter"/> implementation.
        /// </summary>
        /// <param name="item">New <see cref="IAdapter"/> implementation.</param>
        /// <remarks>
        /// Derived classes should override if more events are defined.
        /// </remarks>
        protected virtual void InitializeItem(T item)
        {
            if (item != null)
            {
                // Wire up events
                item.StatusMessage += StatusMessage;
                item.ProcessException += ProcessException;
                item.InputMeasurementKeysUpdated += InputMeasurementKeysUpdated;
                item.OutputMeasurementsUpdated += OutputMeasurementsUpdated;
                item.Disposed += Disposed;

                // Associate parent collection
                item.AssignParentCollection(this);

                // Update adapter routing type flag
                item.ProcessMeasurementFilter = ProcessMeasurementFilter;

                // If automatically initializing new elements, handle object initialization from
                // thread pool so it can take needed amount of time
                if (AutoInitialize)
                    ThreadPool.QueueUserWorkItem(InitializeItem, item);
            }
        }

        // Thread pool delegate to handle item initialization
        private void InitializeItem(object state)
        {
            T item = (T)state;

            try
            {
                item.Initialize();
                item.Initialized = true;
            }
            catch (Exception ex)
            {
                // We report any errors encountered during initialization...
                OnProcessException(ex);
            }
        }

        /// <summary>
        /// Unwires events and disposes of <see cref="IAdapter"/> implementation.
        /// </summary>
        /// <param name="item"><see cref="IAdapter"/> to dispose.</param>
        /// <remarks>
        /// Derived classes should override if more events are defined.
        /// </remarks>
        protected virtual void DisposeItem(T item)
        {
            if (item != null)
            {
                // Un-wire events
                item.StatusMessage -= StatusMessage;
                item.ProcessException -= ProcessException;
                item.InputMeasurementKeysUpdated -= InputMeasurementKeysUpdated;
                item.OutputMeasurementsUpdated -= OutputMeasurementsUpdated;

                // Make sure initialization handles are cleared in case any failed
                // initializations are still pending
                item.Initialized = true;

                // Dissociate parent collection
                item.AssignParentCollection(null);

                // Dipose of item, then un-wire disposed event
                item.Dispose();
                item.Disposed -= Disposed;
            }
        }

        // We monitor the total number of measurements destined for archival here...
        private void m_monitorTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            StringBuilder status = new StringBuilder();
            Ticks currentTime, totalProcessTime;
            long totalNew, processedMeasurements = this.ProcessedMeasurements;

            // Calculate time since last call
            currentTime = DateTime.UtcNow.Ticks;
            totalProcessTime = currentTime - m_lastProcessTime;
            m_totalProcessTime += totalProcessTime.ToSeconds();
            m_lastProcessTime = currentTime;

            // Calculate how many new measurements have been received in the last minute...
            totalNew = processedMeasurements - m_processedMeasurements;
            m_processedMeasurements = processedMeasurements;

            // Process statistics for 12 hours total runtime:
            //
            //          1              1                 1
            // 12345678901234 12345678901234567 1234567890
            // Time span        Measurements    Per second
            // -------------- ----------------- ----------
            // Entire runtime 9,999,999,999,999 99,999,999
            // Last minute         4,985            83

            status.AppendFormat("\r\nProcess statistics for {0} total runtime:\r\n\r\n", m_totalProcessTime.ToString().ToLower());
            status.Append("Time span".PadRight(14));
            status.Append(' ');
            status.Append("Measurements".CenterText(17));
            status.Append(' ');
            status.Append("Per second".CenterText(10));
            status.AppendLine();
            status.Append(new string('-', 14));
            status.Append(' ');
            status.Append(new string('-', 17));
            status.Append(' ');
            status.Append(new string('-', 10));
            status.AppendLine();

            status.Append("Entire runtime".PadRight(14));
            status.Append(' ');
            status.Append(m_processedMeasurements.ToString("N0").CenterText(17));
            status.Append(' ');
            status.Append(((int)(m_processedMeasurements / m_totalProcessTime)).ToString("N0").CenterText(10));
            status.AppendLine();
            status.Append("Last minute".PadRight(14));
            status.Append(' ');
            status.Append(totalNew.ToString("N0").CenterText(17));
            status.Append(' ');
            status.Append(((int)(totalNew / totalProcessTime.ToSeconds())).ToString("N0").CenterText(10));

            // Report updated statistics every minute...
            OnStatusMessage(status.ToString());
        }

        #region [ Explicit IList<IAdapter> Implementation ]

        void ICollection<IAdapter>.Add(IAdapter item)
        {
            lock (this)
            {
                Add((T)item);
            }
        }

        bool ICollection<IAdapter>.Contains(IAdapter item)
        {
            lock (this)
            {
                return Contains((T)item);
            }
        }

        void ICollection<IAdapter>.CopyTo(IAdapter[] array, int arrayIndex)
        {
            lock (this)
            {
                CopyTo(array.Cast<T>().ToArray(), arrayIndex);
            }
        }

        bool ICollection<IAdapter>.Remove(IAdapter item)
        {
            lock (this)
            {
                return Remove((T)item);
            }
        }

        IEnumerator<IAdapter> IEnumerable<IAdapter>.GetEnumerator()
        {
            IAdapter[] adapters;

            lock (this)
            {
                adapters = new IAdapter[Count];

                for (int i = 0; i < Count; i++)
                    adapters[i] = this[i];
            }

            foreach (IAdapter item in adapters)
            {
                yield return item;
            }
        }

        int IList<IAdapter>.IndexOf(IAdapter item)
        {
            lock (this)
            {
                return this.IndexOf((T)item);
            }
        }

        void IList<IAdapter>.Insert(int index, IAdapter item)
        {
            lock (this)
            {
                this.Insert(index, (T)item);
            }
        }

        IAdapter IList<IAdapter>.this[int index]
        {
            get
            {
                lock (this)
                {
                    return this[index];
                }
            }
            set
            {
                lock (this)
                {
                    this[index] = (T)value;
                }
            }
        }

        #endregion

        #endregion
    }
}