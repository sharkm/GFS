﻿//******************************************************************************************************
//  SynchronizedOperation.cs - Gbtc
//
//  Copyright © 2014, Grid Protection Alliance.  All Rights Reserved.
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
//  01/29/2014 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Threading;

namespace GSF.Threading
{
    /// <summary>
    /// Represents an operation that cannot run while it is already in progress.
    /// </summary>
    public class SynchronizedOperation
    {
        #region [ Members ]

        // Constants
        private const int NotRunning = 0;
        private const int Running = 1;
        private const int Pending = 2;

        // Fields
        private Action m_action;
        private Action<Exception> m_exceptionAction;
        private int m_state;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="SynchronizedOperation"/> class.
        /// </summary>
        /// <param name="action">The action to be performed during this operation.</param>
        public SynchronizedOperation(Action action)
            : this(action, null)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SynchronizedOperation"/> class.
        /// </summary>
        /// <param name="action">The action to be performed during this operation.</param>
        /// <param name="exceptionAction">The action to be performed if an exception is thrown from the action.</param>
        public SynchronizedOperation(Action action, Action<Exception> exceptionAction)
        {
            if ((object)action == null)
                throw new ArgumentNullException("action");

            m_action = action;
            m_exceptionAction = exceptionAction;
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Executes the action on this thread or marks the
        /// operation as pending if the operation is already running.
        /// </summary>
        /// <remarks>
        /// When the operation is marked as pending, it will run again after the
        /// operation that is currently running has completed. This is useful if
        /// an update has invalidated the operation that is currently running and
        /// will therefore need to be run again.
        /// </remarks>
        public void RunOnce()
        {
            // lock (m_stateLock)
            // {
            //     if (m_state == NotRunning)
            //         TryRun();
            //     else if (m_state == Running)
            //         m_state = Pending;
            // }

            if (Interlocked.CompareExchange(ref m_state, Pending, Running) == NotRunning)
                TryRun();
        }

        /// <summary>
        /// Executes the action on another thread or marks the
        /// operation as pending if the operation is already running.
        /// </summary>
        /// <remarks>
        /// When the operation is marked as pending, it will run again after the
        /// operation that is currently running has completed. This is useful if
        /// an update has invalidated the operation that is currently running and
        /// will therefore need to be run again.
        /// </remarks>
        public void RunOnceAsync()
        {
            // lock (m_stateLock)
            // {
            //     if (m_state == NotRunning)
            //         TryRunAsync();
            //     else if (m_state == Running)
            //         m_state = Pending;
            // }

            if (Interlocked.CompareExchange(ref m_state, Pending, Running) == NotRunning)
                TryRunAsync();
        }

        /// <summary>
        /// Attempts to execute the action on this thread.
        /// Does nothing if the operation is already running.
        /// </summary>
        public void TryRun()
        {
            // lock (m_stateLock)
            // {
            //     if (m_state == NotRunning)
            //     {
            //         m_state = Running;
            //         ExecuteAction();
            //     }
            // }

            if (Interlocked.CompareExchange(ref m_state, Running, NotRunning) == NotRunning)
                ExecuteAction();
        }

        /// <summary>
        /// Attempts to execute the action on another thread.
        /// Does nothing if the operation is already running.
        /// </summary>
        public void TryRunAsync()
        {
            // lock (m_stateLock)
            // {
            //     if (m_state == NotRunning)
            //     {
            //         m_state = Running;
            //         ThreadPool.QueueUserWorkItem(state => ExecuteAction());
            //     }
            // }

            if (Interlocked.CompareExchange(ref m_state, Running, NotRunning) == NotRunning)
                ThreadPool.QueueUserWorkItem(state => ExecuteAction());
        }

        private void ExecuteAction()
        {
            try
            {
                m_action();
            }
            catch (Exception ex)
            {
                try
                {
                    if ((object)m_exceptionAction != null)
                        m_exceptionAction(ex);
                }
                catch
                {
                }
            }

            // lock (m_stateLock)
            // {
            //     if (m_state == Pending)
            //     {
            //         m_state = Running;
            //         ThreadPool.QueueUserWorkItem(state => ExecuteAction());
            //     }
            //     else if (m_state == Running)
            //     {
            //         m_state = NotRunning;
            //     }
            // }

            if (Interlocked.CompareExchange(ref m_state, NotRunning, Running) == Pending)
            {
                // There is no race condition here because if m_state is Pending,
                // then it cannot be changed by any other line of code except this one
                Interlocked.Exchange(ref m_state, Running);
                ThreadPool.QueueUserWorkItem(state => ExecuteAction());
            }
        }

        #endregion
    }
}
