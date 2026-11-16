using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace APBridgeAddIn
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;

        private ProBridgeService _service;
        private const string PipeName = "ArcGisProBridgePipe";

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static Module1 Current => _this ??= (Module1)FrameworkApplication.FindModule("APBridgeAddIn_Module");

        #region Overrides
        /// <summary>
        /// Called when the module is first loaded. Starts the MCP bridge automatically
        /// so no button click is required.
        /// </summary>
        protected override bool Initialize()
        {
            EnsureServiceStarted();
            return base.Initialize();
        }

        /// <summary>
        /// Starts the named-pipe bridge if it is not already running.
        /// Returns true if it was started by this call, false if it was already running.
        /// </summary>
        public bool EnsureServiceStarted()
        {
            if (_service != null)
                return false;
            _service = new ProBridgeService(PipeName);
            _service.Start();
            return true;
        }

        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //return false to ~cancel~ Application close
            return true;
        }

        /// <summary>
        /// Called when the module is unloaded; shuts the bridge down cleanly.
        /// </summary>
        protected override void Uninitialize()
        {
            _service?.Dispose();
            _service = null;
            base.Uninitialize();
        }

        #endregion Overrides

    }
}
