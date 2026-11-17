// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using UnityEngine;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window holds the Locations
    /// panel's state so it survives domain reloads.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        [SerializeField]
        private AssetPanelState locationsPanelState = new AssetPanelState();
        public AssetPanelState LocationsState { get { return locationsPanelState; } }

    }

    /// <summary>
    /// Locations category. Locations are basic assets, so this uses the common asset
    /// list panel as-is: ID, Name, the template's primary fields, and All Fields.
    /// </summary>
    public class LocationsPanel : AssetListPanel<Location>
    {

        public LocationsPanel() : base("Locations") { }

        protected override string AssetTypeName { get { return "Location"; } }
        protected override string ListPropertyName { get { return "locations"; } }
        protected override DialogueDatabaseEditorWindow.AssetPanelState state { get { return window.LocationsState; } }

        protected override List<Location> GetAssets(DialogueDatabase database) { return database.locations; }
        protected override List<Field> GetTemplateFields(Template template, Location location) { return template.locationFields; }
        protected override List<string> GetPrimaryFieldTitles(Template template, Location location) { return template.locationPrimaryFieldTitles; }

        protected override bool IsSyncEnabled
        {
            get { return database.syncInfo.syncLocations; }
            set { database.syncInfo.syncLocations = value; }
        }

        protected override DialogueDatabase SyncDatabase
        {
            get { return database.syncInfo.syncLocationsDatabase; }
            set { database.syncInfo.syncLocationsDatabase = value; }
        }

        protected override void SyncFromDatabase() { database.SyncLocations(); }

    }

}

#endif
