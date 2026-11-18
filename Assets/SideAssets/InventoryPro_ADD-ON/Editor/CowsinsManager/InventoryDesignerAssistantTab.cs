#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;

namespace cowsins.Inventory
{
    public class InventoryDesignerAssistantTab : ITab
    {
        // ITab related, defines the name of the Tab in the Cowsins Manager
        public string TabName => "Inventory Designer";
        public int Order => 10;
        public bool IsVisible => true;
        private enum ToolMode { Select, Design, Settings, Tutorial }
        private ToolMode currentMode = ToolMode.Design;
        private GUIContent[] toolbarIcons;

        public Padding inventoryPadding = new Padding();
        private GameObject buttonPrefab;
        private int gridRows = 6;
        private int gridCols = 6;

        private Guid[,] itemIDGrid;
        private InventoryGridData_SO selectedInventoryGridData;
        private Vector2Int? selectedSlotCoordinates = null;

        private Item_SO selectedItemToPlace;

        private List<Rect> itemIconRects;
        private List<Item_SO> itemsToDraw;

        private GUIStyle titleStyle, bigHeadingStyle;

        private bool exceedsStack = false;
        private bool showInventoryPadding = false;
        private bool showAttachmentsFoldout;
        private string searchQuery = "";
        private Vector2 itemsScrollPosition;

        InventoryGridData_SO newSelection = null;

        // Stores all the existing Inventory Grid Data files to display them in the Project at the Home Screen
        private List<InventoryGridData_SO> inventoryGridDataInProjectList = new List<InventoryGridData_SO>();
        private List<Item_SO> itemSOList = new List<Item_SO>();

        public void StartTab()
        {
            itemIDGrid = new Guid[gridRows, gridCols];

            InitializeStyles();
            LoadScriptableObjects();
            InitializeInventoryData();
        }
        public void OnGUI()
        {
            if (itemIDGrid == null) StartTab();

            // Reset the rects & items each time the GUI refreshes
            itemIconRects = new List<Rect>();
            itemsToDraw = new List<Item_SO>();
            newSelection = null;

            if (selectedInventoryGridData == null) HomeScreen();
            else NavBar();
            if (newSelection != selectedInventoryGridData)
            {
                selectedInventoryGridData = newSelection;
                InitializeInventoryData();
            }

            if (selectedInventoryGridData == null) return;

            EditorGUILayout.BeginHorizontal();

            // Get the width of the current drawing area
            float halfWindowWidth = EditorGUIUtility.currentViewWidth / 2;
            float buttonSize = halfWindowWidth / selectedInventoryGridData.columns;

            DrawGrid(halfWindowWidth, buttonSize);
            SideMenu(halfWindowWidth);

            EditorGUILayout.EndHorizontal();

        }

        private void HomeScreen()
        {
            if (inventoryGridDataInProjectList.Count == 0) LoadInventoryGridDataInProject();

            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("NotificationBackground"));

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();
            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/InventoryDesigner_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture, GUILayout.Width(500));

            GUILayout.Space(15);
            GUILayout.Label("Select Available Inventory Grid Data SOs in Project:", GUILayout.Width(350));
            GUILayout.Space(5);

            itemsScrollPosition = EditorGUILayout.BeginScrollView(itemsScrollPosition, GUILayout.Width(300), GUILayout.Height(160));

            foreach (var gridData in inventoryGridDataInProjectList)
            {
                GUIStyle invGridDataButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 1, 1)
                };

                if (GUILayout.Button(gridData.name, invGridDataButtonStyle, GUILayout.Width(280), GUILayout.Height(30)))
                {
                    newSelection = gridData;
                }
            }

            EditorGUILayout.EndScrollView();
            GUILayout.Space(15);

            GUIStyle createButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                padding = new RectOffset(10, 10, 1, 1)
            };


            if (GUILayout.Button("Create New Inventory Grid Data", createButtonStyle, GUILayout.Height(30))) CreateNewInventoryGridData();

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.EndVertical();
        }

        private void NavBar()
        {
            GUILayout.BeginHorizontal(GUI.skin.GetStyle("HelpBox"));

            GUILayout.Label("Select Inventory Grid Data:", GUILayout.Width(170));

            newSelection = (InventoryGridData_SO)EditorGUILayout.ObjectField(selectedInventoryGridData, typeof(InventoryGridData_SO), false);

            if (GUILayout.Button("Close", GUILayout.Width(100)))
            {
                newSelection = null;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void DrawGrid(float halfWindowWidth, float buttonSize)
        {
            EditorGUILayout.BeginVertical();
            for (int row = 0; row < selectedInventoryGridData.rows; row++)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(halfWindowWidth));

                for (int col = 0; col < selectedInventoryGridData.columns; col++)
                {
                    if (IsSlotSelected(row, col))
                        GUI.backgroundColor = Color.blue;
                    else if (IsSlotOccupied(row, col))
                        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                    else
                        GUI.backgroundColor = Color.grey; // Empty
                    if (GUILayout.Button("", GUILayout.Width(buttonSize), GUILayout.Height(buttonSize)))
                    {
                        if (Event.current.button == 0)
                        {
                            if (currentMode == ToolMode.Design && selectedItemToPlace != null) AttemptToPlaceItem(row, col);
                            else if (currentMode == ToolMode.Select) SelectItemAt(row, col);
                        }
                        else if (Event.current.button == 1) RemoveItemAt(row, col);
                    }

                    int index = row * selectedInventoryGridData.columns + col;
                    if (index < selectedInventoryGridData.inventoryInitialData.Length)
                    {
                        var inventoryData = selectedInventoryGridData.inventoryInitialData[index];

                        if (inventoryData?.item && inventoryData.item.icon != null && IsTopLeftOfItem(row, col))
                        {
                            Vector2Int itemSize = inventoryData.item.itemSize;

                            Rect iconRect = new Rect(
                                GUILayoutUtility.GetLastRect().x,
                                GUILayoutUtility.GetLastRect().y,
                                buttonSize * itemSize.x,
                                buttonSize * itemSize.y
                            );

                            itemIconRects.Add(iconRect);
                            itemsToDraw.Add(inventoryData.item);
                        }
                    }
                }

                GUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            // Now draw the icons for each item on the grid
            DrawItemIcons();
        }

        private void SideMenu(float halfWindowWidth)
        {
            // Render the additional information next to the grid
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"), GUILayout.Width(halfWindowWidth * .91f));

            currentMode = (ToolMode)GUILayout.Toolbar((int)currentMode, toolbarIcons);

            GUILayout.Space(10);
            if (currentMode == ToolMode.Select) SelectTab();
            else if (currentMode == ToolMode.Design) DesignTab(100);
            else if (currentMode == ToolMode.Settings) SettingsTab();
            else if (currentMode == ToolMode.Tutorial) TutorialsTab();

            EditorGUILayout.EndVertical();
        }
        private void SelectTab()
        {
            if (selectedSlotCoordinates == null)
            {
                EditorGUILayout.HelpBox("Selected a Slot to modify its properties.", MessageType.Info);
                return;
            }
            Vector2Int selected = selectedSlotCoordinates.Value;
            SlotData selectedData = selectedInventoryGridData.inventoryInitialData[selected.x * selectedInventoryGridData.columns + selected.y];

            GUILayout.Label($"SLOT [{selected.x},{selected.y}] PROPERTIES", titleStyle);
            GUILayout.Space(20);

            if (selectedData.item != null)
            {

                Sprite itemIcon = selectedData.item.icon; // Assuming `icon` is of type Sprite

                // Display the icon and name
                GUILayout.BeginHorizontal();
                {
                    if (itemIcon != null)
                    {
                        // Convert Sprite to Texture2D
                        Texture2D iconTexture = itemIcon.texture;
                        GUILayout.Label(iconTexture, GUILayout.Width(100), GUILayout.Height(100)); // Display the icon with a fixed size
                    }
                    else
                    {
                        GUILayout.Label("No Icon", GUILayout.Width(50), GUILayout.Height(50)); // Placeholder if no icon is available
                    }

                    GUILayout.Space(5);

                    GUILayout.BeginVertical();

                    GUILayout.Label($"Item: {selectedData.item._name}");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Amount:");
                    selectedData.amount = EditorGUILayout.IntField(selectedData.amount);
                    if (selectedData.amount > selectedData.item.maxStack)
                    {
                        selectedData.amount = selectedData.item.maxStack;
                        exceedsStack = true;
                    }

                    GUILayout.EndHorizontal();
                    if (exceedsStack) EditorGUILayout.HelpBox("Amount can´t be greater than the maximum allowed stack of the item.", MessageType.Error);
                    if (GUILayout.Button("Open Item"))
                    {
                        EditorUtility.OpenPropertyEditor(selectedData.item);
                    }
                    GUILayout.EndVertical();
                }

                GUILayout.EndHorizontal();

                if (selectedData.item is Weapon_SO weapon)
                {
                    int totalBullets = weapon.limitedMagazines ? weapon.magazineSize * weapon.totalMagazines : weapon.magazineSize;

                    GUILayout.Label("WEAPON PROPERTIES", titleStyle);

                    GUILayout.Space(10);

                    GUILayout.Label($"Bullets in Magazine: {weapon.magazineSize}");
                    GUILayout.Label($"Total Bullets: {totalBullets}");

                    EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                    {
                        // Attachments foldout
                        showAttachmentsFoldout = EditorGUILayout.Foldout(showAttachmentsFoldout, "Default Attachments", true);
                        if (showAttachmentsFoldout)
                        {
                            EditorGUI.indentLevel++;
                            DrawAttachmentGrid(
                                new Attachment[]
                                {
                                    weapon.weaponObject.AttachmentState.GetDefault(AttachmentType.Barrel),
                                    weapon.weaponObject.AttachmentState.GetDefault(AttachmentType.Scope),
                                    weapon.weaponObject.AttachmentState.GetDefault(AttachmentType.Stock),
                                    weapon.weaponObject.AttachmentState.GetDefault(AttachmentType.Grip),
                                    weapon.weaponObject.AttachmentState.GetDefault(AttachmentType.Magazine),
                                    weapon.weaponObject.AttachmentState.GetDefault(AttachmentType.Flashlight),
                                    weapon.weaponObject.AttachmentState.GetDefault(AttachmentType.Laser)
                                },
                                new string[]
                                {
                                    "Barrel", "Scope", "Stock", "Grip", "Magazine", "Flashlight", "Laser"
                                }
                            );
                            GUILayout.Space(15);
                            EditorGUI.indentLevel--;
                        }
                    }
                    EditorGUILayout.EndVertical();
                }

                EditorUtility.SetDirty(selectedInventoryGridData);
            }
            else
            {
                EditorGUILayout.HelpBox("Selected Slot is empty.", MessageType.Warning);
            }
        }

        private void DrawAttachmentGrid(Attachment[] attachments, string[] labels)
        {
            int columns = 4;
            int cellSize = 80;
            int padding = 5;

            int count = 0;

            GUILayout.Space(10);
            EditorGUILayout.BeginVertical();

            while (count < attachments.Length)
            {
                EditorGUILayout.BeginHorizontal();

                for (int i = 0; i < columns; i++)
                {
                    // Avoid rendering more attachment slots than available ( grid size = 8 > attachments count = 7 )
                    if (count >= attachments.Length)
                    {
                        GUILayout.Space(cellSize);
                        continue;
                    }

                    var attachment = attachments[count];
                    var label = labels[count];
                    AttachmentIdentifier_SO id = attachment?.attachmentIdentifier;

                    Rect cellRect = GUILayoutUtility.GetRect(cellSize, cellSize, GUILayout.ExpandWidth(false));

                    // Handle Button
                    // Disable GUI if there is no Attachment 
                    if(id == null)
                    {
                        GUI.enabled = false;
                    }
                    if (GUI.Button(cellRect, GUIContent.none) && id != null)
                    {
                        EditorUtility.OpenPropertyEditor(id);
                    }
                    GUI.enabled = true;

                    // Draw icon & Attachment Name inside the cellRect
                    GUI.BeginGroup(cellRect);

                    float iconSize = 32;
                    var centeredStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 10,
                        wordWrap = true
                    };

                    if (id?.icon != null)
                    {
                        Rect spriteRect = id.icon.rect;
                        Texture2D tex = id.icon.texture;
                        Rect texCoords = new Rect(
                            spriteRect.x / tex.width,
                            spriteRect.y / tex.height,
                            spriteRect.width / tex.width,
                            spriteRect.height / tex.height
                        );

                        Rect iconRect = new Rect((cellSize - iconSize) / 2, padding, iconSize, iconSize);
                        GUI.DrawTextureWithTexCoords(iconRect, tex, texCoords);
                    }

                    // Display Open Attachment Text
                    if (id != null)
                    {
                        GUI.Label(new Rect(0, iconSize + padding + 5, cellSize, 18), "Open", centeredStyle);
                        GUI.Label(new Rect(0, iconSize + padding + 20, cellSize, 18), id.name, centeredStyle);
                    }
                    else
                    {
                        GUI.Label(new Rect(0, iconSize + padding + 10, cellSize, 36), $"No {label}", centeredStyle);
 
                    }

                    GUI.EndGroup();
                    count++;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }



        private void DesignTab(float buttonSize)
        {
            if (selectedItemToPlace == null)
            {
                titleStyle.normal.textColor = Color.red;
                GUILayout.Label($"SELECTED ITEM: NULL", titleStyle);
                titleStyle.normal.textColor = Color.white;
                GUILayout.Space(5);
                EditorGUILayout.HelpBox("Select an Item from the ITEMS LIST.", MessageType.Info);
            }
            else
            {
                GUILayout.Label($"SELECTED ITEM: {selectedItemToPlace._name.ToUpper()}", titleStyle);
                GUILayout.Space(5);
                EditorGUILayout.HelpBox("Click on an empty slot to place the selected item.", MessageType.Info);

                Sprite itemIcon = selectedItemToPlace.icon; // Assuming `icon` is of type Sprite

                if (itemIcon != null)
                {
                    // Convert Sprite to Texture2D
                    Texture2D iconTexture = itemIcon.texture;
                    GUILayout.BeginHorizontal(); // Begin horizontal group
                    GUILayout.FlexibleSpace();  // Add flexible space to push content to the center
                    GUILayout.Label(iconTexture, GUILayout.Width(75), GUILayout.Height(75)); // Display the icon with a fixed size
                    GUILayout.FlexibleSpace();  // Add flexible space to push content to the center
                    GUILayout.EndHorizontal();  // End horizontal group
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("No Icon", GUILayout.Width(50), GUILayout.Height(50)); // Placeholder if no icon is available
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }


            int hoveredIndex = -1;
            Rect hoveredRect = new Rect();
            int gridColumns = 3;
            float buttonPadding = 5;

            // Filter the itemSOList based on the search query
            var filteredItemList = string.IsNullOrEmpty(searchQuery)
                ? itemSOList
                : itemSOList.Where(item => item.name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            itemsScrollPosition = GUILayout.BeginScrollView(itemsScrollPosition);

            GUILayout.Space(10);
            GUILayout.Label("ITEMS LIST", titleStyle);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            searchQuery = GUILayout.TextField(searchQuery);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            for (int i = 0; i < filteredItemList.Count; i += gridColumns)
            {
                GUILayout.BeginHorizontal();

                for (int j = i; j < i + gridColumns && j < filteredItemList.Count; j++)
                {
                    Item_SO item = filteredItemList[j];
                    Texture2D icon = AssetPreview.GetAssetPreview(item?.icon);

                    Rect buttonRect = GUILayoutUtility.GetRect(buttonSize, buttonSize);
                    GUIContent buttonContent = new GUIContent(icon, item?.name);

                    if (GUI.Button(buttonRect, buttonContent))
                    {
                        // Set the selected item when button is clicked
                        selectedItemToPlace = item;
                    }
                    // Detect hover
                    if (buttonRect.Contains(Event.current.mousePosition))
                    {
                        hoveredIndex = j;
                        hoveredRect = buttonRect;
                    }


                    // Draw the Button
                    GUI.Button(buttonRect, buttonContent);
                    GUILayout.Space(buttonPadding);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(buttonPadding);
            }

            // Draw overlay and weapon name if hovering
            if (hoveredIndex != -1)
            {
                Item_SO hoveredWeapon = filteredItemList[hoveredIndex];

                // Draw semi-transparent black overlay
                Color overlayColor = new Color(0, 0, 0, 0.5f);
                EditorGUI.DrawRect(hoveredRect, overlayColor);

                // Draw weapon name
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = new GUIStyleState() { textColor = Color.white }
                };

                GUI.Label(hoveredRect, $"{hoveredWeapon.name}", labelStyle);
            }

            GUILayout.EndScrollView();


        }
        private bool changesMade = false;

        private void SettingsTab()
        {
            if (buttonPrefab == null)
                EditorGUILayout.HelpBox("BUTTON PREFAB CANNOT BE NULL.", MessageType.Error);

            GUILayout.Label("GRID STYLE", titleStyle);

            EditorGUI.BeginChangeCheck(); // Start tracking changes

            buttonPrefab = (GameObject)EditorGUILayout.ObjectField("Button Prefab", buttonPrefab, typeof(GameObject), false);
            if (buttonPrefab == null)
            {
                GUILayout.Space(5);
                if (GUILayout.Button("Assign Default Button Prefab", GUILayout.Height(30)))
                {
                    GameObject inventoryProSlot = Resources.Load("InventoryProSlot") as GameObject;
                    if (inventoryProSlot != null) buttonPrefab = inventoryProSlot;
                    else Debug.LogError("<color=red>[COWSINS]</color> InventoryProSlot couldn´t be found in Resources folder. Did you rename the Prefab or move it outside the " +
                        "Resources folder?");
                }
            }

            DrawPaddingField("Padding", ref inventoryPadding, ref showInventoryPadding);
            GUILayout.Space(10);
            GUILayout.Space(10);
            GUILayout.Label("GRID SIZE", titleStyle);

            gridRows = (int)EditorGUILayout.Slider("Rows", gridRows, 1, 10);
            gridCols = (int)EditorGUILayout.Slider("Columns", gridCols, 1, 10);

            if (GUILayout.Button($"Reset Grid Size to {selectedInventoryGridData.rows}:{selectedInventoryGridData.columns}", GUILayout.Height(30)))
                ResetGridSize();

            if (EditorGUI.EndChangeCheck()) // Detects if any value changed
                changesMade = true;

            if (changesMade)
                EditorGUILayout.HelpBox("You have unsaved changes. Click 'Apply Changes' to save them.", MessageType.Warning);

            if (GUILayout.Button("Apply Changes", GUILayout.Height(30)))
            {
                ApplyChanges();
                changesMade = false;
            }
        }

        private void DrawPaddingField(string label, ref Padding padding, ref bool foldoutState)
        {
            foldoutState = EditorGUILayout.Foldout(foldoutState, label, true);

            if (foldoutState)
            {
                EditorGUI.indentLevel++;
                padding.horizontal = EditorGUILayout.FloatField("Horizontal", padding.horizontal);
                padding.vertical = EditorGUILayout.FloatField("Vertical", padding.vertical);
                EditorGUI.indentLevel--;
            }
        }

        private void TutorialsTab()
        {
            bigHeadingStyle.alignment = TextAnchor.UpperCenter;
            GUILayout.Label("KEYBINDS", bigHeadingStyle);
            GUILayout.Label("LMB: Select or Create an Item from the Grid", titleStyle);
            GUILayout.Label("RMB: Delete an Item from the Grid", titleStyle);

            GUILayout.Space(10);

            GUILayout.Label("TUTORIALS & SUPPORT", bigHeadingStyle);

            bigHeadingStyle.alignment = TextAnchor.MiddleLeft;

            GUIStyle wrapStyle = new GUIStyle(GUI.skin.label);
            wrapStyle.wordWrap = true;

            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                if (GUILayout.Button("[RELATED DOCUMENTATION] INVENTORY DESIGNER", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://cowsinss-organization.gitbook.io/inventory-pro-add-on-documentation/how-to-use-and-guides/inventory-designer-creating-updating-and-deleting-inventory-grid-data");
                }
                GUILayout.Space(5);
                GUILayout.Label("Inventory Grid Data contains information regarding the initial storage configuration of the player's inventory and any chests. " +
                    "You can create as many Inventory Grid Data entries as needed. While only one can be assigned to the player’s inventory, you can create multiple " +
                    "Inventory Grid Data entries and assign them to different chests, allowing each chest to contain its own unique initial data.", wrapStyle);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);
            EditorGUILayout.HelpBox("Cowsins is currently facing some issues with the YouTube channel. Tutorials have been backed up on cowsins.com.", MessageType.Warning);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(.5f, .5f, .5f, 1);

            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/documentationIcon"), "Documentation", "https://cowsinss-organization.gitbook.io/inventory-pro-add-on-documentation", .42f, .3f);
            GUILayout.FlexibleSpace();
            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/tutorialsIcon"), "Tutorials", "https://www.cowsins.com/", .42f, .3f);
            GUILayout.FlexibleSpace();
            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/supportIcon"), "Support", "https://discord.gg/759gSeTT9m", .42f, .3f);
            GUILayout.Space(10);

            GUILayout.EndHorizontal();
        }
        private void InitializeInventoryData()
        {
            // Ensure selectedInventoryGridData is not null
            if (selectedInventoryGridData == null) return;
            // Initialize inventoryInitialData if it's null
            if (selectedInventoryGridData.inventoryInitialData == null || selectedInventoryGridData.inventoryInitialData.Length == 0)
                selectedInventoryGridData.inventoryInitialData = new SlotData[selectedInventoryGridData.rows * selectedInventoryGridData.columns];

            if (itemIDGrid == null) itemIDGrid = new Guid[gridRows, gridCols];

            // Loop through gridRows and gridCols
            for (int row = 0; row < selectedInventoryGridData.rows; row++)
            {
                for (int col = 0; col < selectedInventoryGridData.columns; col++)
                {
                    // Ensure SlotData is not null
                    if (selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col] == null)
                        selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col] = new SlotData();

                    if (itemIDGrid[row, col] == null || itemIDGrid[row, col] == Guid.Empty)
                        itemIDGrid[row, col] = Guid.NewGuid();

                    if (selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col].item != null)
                    {
                        Vector2Int itemSize = selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col].item.itemSize; // Get the actual item size

                        for (int i = 0; i < itemSize.y; i++) // height
                        {
                            for (int j = 0; j < itemSize.x; j++) // width
                            {
                                int checkRow = row + i;
                                int checkCol = col + j;

                                // Avoid out of bounds
                                if (checkRow < selectedInventoryGridData.rows && checkCol < selectedInventoryGridData.columns)
                                    itemIDGrid[checkRow, checkCol] = itemIDGrid[row, col];
                            }
                        }
                    }
                }
            }

            buttonPrefab = selectedInventoryGridData.buttonPrefab;
            inventoryPadding = selectedInventoryGridData.inventoryPadding;
            gridRows = selectedInventoryGridData.rows;
            gridCols = selectedInventoryGridData.columns;
        }

        private void SelectItemAt(int row, int col)
        {
            // Deselects current fields
            GUI.FocusControl(null);

            Guid itemID = itemIDGrid[row, col];

            if (itemID != Guid.Empty) // Check if the clicked cell contains an item
            {
                // Find the top-left corner of the item
                Vector2Int topLeft = FindTopLeftCorner(row, col, itemID);

                if (topLeft != null)
                {
                    selectedSlotCoordinates = topLeft; // Store the top-left corner as the selected slot
                }

                exceedsStack = false;
            }
            else
            {
                selectedSlotCoordinates = null;
            }
        }

        private Vector2Int FindTopLeftCorner(int row, int col, Guid itemID)
        {
            // Traverse upwards and to the left to find the top-left corner of the item
            while (row > 0 && itemIDGrid[row - 1, col] == itemID) row--;
            while (col > 0 && itemIDGrid[row, col - 1] == itemID) col--;

            return new Vector2Int(row, col);
        }

        private bool IsSlotOccupied(int row, int col)
        {
            int index = row * selectedInventoryGridData.columns + col;
            if (index >= selectedInventoryGridData.inventoryInitialData.Length)
                return false;

            // If the cell itself has an item, it’s occupied.
            if (selectedInventoryGridData.inventoryInitialData[index]?.item != null)
                return true;

            // Otherwise, find the top-left corner of the item occupying this cell.
            Vector2Int topLeft = FindTopLeftCorner(row, col, itemIDGrid[row, col]);
            int topLeftIndex = topLeft.x * selectedInventoryGridData.columns + topLeft.y;
            return selectedInventoryGridData.inventoryInitialData[topLeftIndex]?.item != null;
        }

        private void DrawItemIcons()
        {
            for (int i = 0; i < itemIconRects.Count; i++)
            {
                Rect rect = itemIconRects[i];
                Item_SO item = itemsToDraw[i];

                if (item.icon != null)
                    GUI.DrawTexture(rect, item.icon.texture, ScaleMode.ScaleToFit);
            }
        }

        private void AttemptToPlaceItem(int startRow, int startCol)
        {
            Vector2Int selectionSize = selectedItemToPlace.itemSize;

            if (!CanPlaceItem(startRow, startCol, selectionSize)) return;

            Guid newItemID = Guid.NewGuid();
            SlotData newSlotData = new SlotData(selectedItemToPlace, 1);

            // Place the item in the grid based on its size
            for (int i = 0; i < selectionSize.y; i++) // Use height from item size
            {
                for (int j = 0; j < selectionSize.x; j++) // Use width from item size
                {
                    int row = startRow + i;
                    int col = startCol + j;

                    // Check bounds before placing the item
                    if (row < selectedInventoryGridData.rows && col < selectedInventoryGridData.columns)
                    {
                        selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col].item = selectedItemToPlace;
                        if(selectedItemToPlace is Weapon_SO)
                        {
                            Weapon_SO weapon = (Weapon_SO)selectedItemToPlace;
                            selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col].bulletsLeftInMagazine 
                                = weapon.magazineSize;
                            selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col].totalBullets
                                = weapon.limitedMagazines ? weapon.magazineSize * weapon.totalMagazines : weapon.magazineSize;
                        }
                        itemIDGrid[row, col] = newItemID; // Assign the same item ID to all cells in the selection

                        if (i == 0 && j == 0) selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col] = newSlotData;
                        else selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col] = new SlotData(); // Empty SlotData for other parts
                    }
                }
            }
            // Save changes
            EditorUtility.SetDirty(selectedInventoryGridData);
        }


        private bool CanPlaceItem(int startRow, int startCol, Vector2Int itemSize)
        {
            // Check if the item can fit in the selected area (if it's empty and within bounds)
            for (int i = 0; i < itemSize.y; i++)
            {
                for (int j = 0; j < itemSize.x; j++)
                {
                    int row = startRow + i;
                    int col = startCol + j;

                    // The item can't be placed here
                    if (row >= selectedInventoryGridData.rows || col >= selectedInventoryGridData.columns
                        || selectedInventoryGridData.inventoryInitialData[row * selectedInventoryGridData.columns + col]?.item != null) return false;
                }
            }

            return true;
        }

        private void RemoveItemAt(int row, int col)
        {
            Guid itemID = itemIDGrid[row, col];

            if (itemID != Guid.Empty)
            {
                for (int i = 0; i < selectedInventoryGridData.rows; i++)
                {
                    for (int j = 0; j < selectedInventoryGridData.columns; j++)
                    {
                        if (itemIDGrid[i, j] == itemID)
                        {
                            itemIDGrid[i, j] = Guid.NewGuid();
                            selectedInventoryGridData.inventoryInitialData[i * selectedInventoryGridData.columns + j] = new SlotData(); // Clear SlotData
                        }
                    }
                }

                // Mark the ScriptableObject as dirty to save changes
                EditorUtility.SetDirty(selectedInventoryGridData);
            }
        }

        private bool IsSlotSelected(int row, int col)
        {
            return selectedSlotCoordinates.HasValue && selectedSlotCoordinates.Value.x == row && selectedSlotCoordinates.Value.y == col && currentMode == ToolMode.Select;
        }
        private bool IsTopLeftOfItem(int row, int col)
        {
            Guid itemID = itemIDGrid[row, col];
            return itemID != Guid.Empty && (row == 0 || itemIDGrid[row - 1, col] != itemID) && (col == 0 || itemIDGrid[row, col - 1] != itemID);
        }

        private void ResetGridSize()
        {
            if (selectedInventoryGridData == null) return;
            gridRows = selectedInventoryGridData.rows;
            gridCols = selectedInventoryGridData.columns;
        }

        private void ApplyChanges()
        {
            if (selectedInventoryGridData == null) return;
            if (buttonPrefab == null)
            {
                EditorUtility.DisplayDialog(
                        "Error Applying Changes!",
                        "Button Prefab is missing, please assign a valid Button Prefab.",
                        "OK"
                    );
                return;
            }
            bool updateGrid = gridRows != selectedInventoryGridData.rows || gridCols != selectedInventoryGridData.columns;
            if (updateGrid)
            {
                if (!EditorUtility.DisplayDialog(
                        "Apply Changes",
                        "Adjusting these settings will reset your Inventory Items.",
                        "Ok", "Back")) return;
            }

            selectedInventoryGridData.rows = gridRows;
            selectedInventoryGridData.columns = gridCols;
            selectedInventoryGridData.buttonPrefab = buttonPrefab;
            if (updateGrid)
            {
                itemIDGrid = new Guid[gridRows, gridCols];
                selectedInventoryGridData.inventoryInitialData = new SlotData[gridRows * gridCols];
            }
            InitializeInventoryData();
            // Save Changes
            EditorUtility.SetDirty(selectedInventoryGridData);
        }
        private void LoadScriptableObjects()
        {
            itemSOList = AssetDatabase.FindAssets("t:Item_SO")
                                      .Select(guid => AssetDatabase.LoadAssetAtPath<Item_SO>(AssetDatabase.GUIDToAssetPath(guid)))
                                      .Where(item => item != null)
                                      .ToList();
        }

        private void LoadInventoryGridDataInProject()
        {
            inventoryGridDataInProjectList = AssetDatabase.FindAssets("t:InventoryGridData_SO")
                                      .Select(guid => AssetDatabase.LoadAssetAtPath<InventoryGridData_SO>(AssetDatabase.GUIDToAssetPath(guid)))
                                      .Where(item => item != null)
                                      .ToList();
        }
        private void CreateNewInventoryGridData()
        {
            // Create a new instance of InventoryGridData_SO
            InventoryGridData_SO newGridData = ScriptableObject.CreateInstance<InventoryGridData_SO>();

            // Define the path to save the new asset
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Inventory Grid Data",
                "NewInventoryGridData.asset",
                "asset",
                "Please enter a file name to save the Inventory Grid Data.");

            if (string.IsNullOrEmpty(path))
                return;

            // Save the new asset
            AssetDatabase.CreateAsset(newGridData, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Reload the list to include the new asset
            LoadInventoryGridDataInProject();
        }

        private void InitializeStyles()
        {
            toolbarIcons = new GUIContent[]
            {
            new GUIContent(EditorGUIUtility.IconContent("Grid.Default").image, "Select Mode"),
            new GUIContent(EditorGUIUtility.IconContent("Grid.PaintTool").image, "Design Mode"),
            new GUIContent(EditorGUIUtility.IconContent("CustomTool").image, "Settings Mode"),
            new GUIContent(EditorGUIUtility.IconContent("console.infoicon.sml").image, "Settings Mode")
            };

            titleStyle = CowsinsEditorWindowUtilities.TitleStyle();
            bigHeadingStyle = CowsinsEditorWindowUtilities.BigHeadingStyle();
        }
    }
}

#endif