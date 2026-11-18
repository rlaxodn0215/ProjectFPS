#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace cowsins.Inventory
{
    public class RecipesTab : ITab
    {
        public string TabName => "Recipes & Items";
        public int Order => 10;
        public bool IsVisible => true;
        private int selectedTab = 0;
        private string searchQuery = "";
        private string recipesSearchQuery = "";

        // Item and Recipe Lists
        private List<Item_SO> itemSOList = new List<Item_SO>();
        private List<Recipe_SO> recipesSOList = new List<Recipe_SO>();
        private List<Recipe_SO.Ingredient> currentIngredients = new List<Recipe_SO.Ingredient>();

        // Scroll Positions
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 recipesScrollPosition = Vector2.zero;
        private Vector2 ingredientsScrollPosition = Vector2.zero;

        // Crafting Data
        private Recipe_SO recipeToModify = null;
        private int minimumPlayerLevelRequired;
        private int resultAmount;
        private float timeToCraft;
        private Item_SO resultItem = null;

        // Layout
        private int gridColumns = 4;
        private int buttonPadding = 5;
        private GUIStyle titleStyle;
        private GUIStyle bigHeadingStyle;

        // Helper Feature
        private bool problematicRecipesFound = false;

        public void StartTab() => InitializeStyles();
        public void OnGUI()
        {
            if(bigHeadingStyle == null) StartTab();

            problematicRecipesFound = false; 

            if (itemSOList.Count == 0)
                LoadScriptableObjects();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            InventoryItemsLeftSide(); 
            RecipesRightSide();
            GUILayout.EndHorizontal(); 
        }

        private void InventoryItemsLeftSide()
        {
            float leftSectionWidth = EditorGUIUtility.currentViewWidth * 0.47f;
            int buttonSize = (int)((leftSectionWidth - (gridColumns - 1) * buttonPadding) / gridColumns);
            int hoveredIndex = -1;
            Rect hoveredRect = new Rect();

            // Left section: Inventory items grid
            GUILayout.BeginVertical(GUILayout.Width(leftSectionWidth));
            GUILayout.Label("Inventory Items", bigHeadingStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            searchQuery = GUILayout.TextField(searchQuery, GUILayout.Width(leftSectionWidth * .8f));
            GUILayout.EndHorizontal();
            GUILayout.Space(5);


            // Filter the itemSOList based on the search query
            var filteredItemList = string.IsNullOrEmpty(searchQuery)
                ? itemSOList
                : itemSOList.Where(item => item.name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(leftSectionWidth));

            // Draws all the items in the Project in a Grid
            for (int i = 0; i < filteredItemList.Count; i += gridColumns)
            {
                GUILayout.BeginHorizontal();

                for (int j = i; j < i + gridColumns && j < filteredItemList.Count; j++)
                {
                    Item_SO item = filteredItemList[j];
                    Texture2D icon = AssetPreview.GetAssetPreview(item?.icon);

                    Rect buttonRect = GUILayoutUtility.GetRect(buttonSize, buttonSize);
                    GUIContent buttonContent = new GUIContent(icon, item?.name);

                    // Drag Start & Handle Dragging
                    if (Event.current.type == EventType.MouseDrag && buttonRect.Contains(Event.current.mousePosition))
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new UnityEngine.Object[] { item }; // Attach the item being dragged
                        DragAndDrop.StartDrag($"Dragging {item.name}");
                        Event.current.Use();
                    }

                    // Detect hover
                    if (buttonRect.Contains(Event.current.mousePosition))
                    {
                        // Stores the index and rect of the current hovered button
                        hoveredIndex = j;
                        hoveredRect = buttonRect;
                    }
                    // Draw the Button
                    GUI.Button(buttonRect, buttonContent);
                }

                GUILayout.EndHorizontal();
            }

            // Draw overlay and weapon name if hovering
            if (hoveredIndex != -1)
            {
                Item_SO hoveredWeapon = filteredItemList[hoveredIndex];

                // Draw black overlay on top of the button to show it is being hovered. 
                Color overlayColor = new Color(0, 0, 0, 0.5f);
                EditorGUI.DrawRect(hoveredRect, overlayColor);

                // Weapon name
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = new GUIStyleState() { textColor = Color.white }
                };

                GUI.Label(hoveredRect, $"{hoveredWeapon.name}", labelStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
        private void RecipesRightSide()
        {
            float rightSectionWidth = EditorGUIUtility.currentViewWidth * 0.5f;

            GUILayout.BeginVertical(GUILayout.Width(rightSectionWidth));

            if (recipeToModify)
            {
                GUILayout.BeginHorizontal();
                // Return Button
                if (GUILayout.Button(new GUIContent("<"), GUILayout.Width(20)))
                {
                    recipeToModify = null;
                }
                GUILayout.Label("MODIFY A RECIPE", EditorStyles.boldLabel);
                GUILayout.EndHorizontal();
            
                RecipeHandling(true, rightSectionWidth * .7f);
            }
            else
            {
                string[] tabNames = { "RECIPES LIST", "CREATE RECIPE", "TUTORIALS" };
                int previousTab = selectedTab;
                selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Width(rightSectionWidth));

                // Clear ingredients & result if clicking on Create Recipe
                if (selectedTab == 1 && previousTab != 1)
                {        
                    // Deselects current fields
                    GUI.FocusControl(null);
                    currentIngredients.Clear();
                    resultItem = null;
                }

                switch (selectedTab)
                {
                    case 0:
                        RecipesList(rightSectionWidth);
                        break;
                    case 1:
                        RecipeHandling(false, rightSectionWidth);
                        break;
                    case 2:
                        TutorialsTab();
                        break;
                }
            }

            GUILayout.EndVertical();
        }

        private void RecipesList(float rightSectionWidth)
        {
            GUILayout.Label("Recipes List", bigHeadingStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search Recipe:", GUILayout.Width(120));
            recipesSearchQuery = GUILayout.TextField(recipesSearchQuery, GUILayout.Width(rightSectionWidth * 0.5f));
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            if(recipesSOList.Count <= 0)
            {      
                GUILayout.Label("It looks like there are no recipes yet!", titleStyle);
            }

            var filteredRecipeList = string.IsNullOrEmpty(recipesSearchQuery)
                ? recipesSOList
                : recipesSOList.Where(item => item.result.name.IndexOf(recipesSearchQuery, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Show a vertical list of all recipes with buttons displaying their result and ingredient icons
            recipesScrollPosition = GUILayout.BeginScrollView(recipesScrollPosition, GUILayout.Width(rightSectionWidth));

            for (int i = 0; i < filteredRecipeList.Count; i++)
            {
                Recipe_SO recipe = filteredRecipeList[i];

                GUIStyle backgroundStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = Texture2D.grayTexture },
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(5, 5, 10, 10)
                };

                GUILayout.BeginHorizontal(backgroundStyle);
                GUILayout.BeginVertical();

                if(recipe.result == null)
                {
                    problematicRecipesFound = true; 
                    EditorGUILayout.HelpBox($"No result was detected in this Recipe.\nPlease ensure the recipe is configured correctly.", MessageType.Error);
                    GUILayout.Label("Unnamed Recipe", EditorStyles.boldLabel, GUILayout.Width(150));
                }
                else
                {
                    // Display result name at the top
                    GUILayout.Label(recipe.result.name, EditorStyles.boldLabel, GUILayout.Width(150));
                }


                GUILayout.BeginHorizontal();

                // Display result icon if available, otherwise display a warning icon
                Texture2D resultIcon = AssetPreview.GetAssetPreview(recipe.result?.icon);
                if (resultIcon == null) resultIcon = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D; 
                else GUILayout.Label(resultIcon, GUILayout.Width(60), GUILayout.Height(60));

                // Display ingredient icons horizontally if available, otherwise display a warning icon
                foreach (var ingredient in recipe.ingredients)
                {
                    Texture2D ingredientIcon = AssetPreview.GetAssetPreview(ingredient.item?.icon);
                    if (ingredientIcon != null)
                        GUILayout.Label(ingredientIcon, GUILayout.Width(30), GUILayout.Height(30));
                    else
                        GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon").image as Texture2D, GUILayout.Width(30), GUILayout.Height(30));
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();

                // Edit Button
                if (GUILayout.Button(EditorGUIUtility.IconContent("Grid.PaintTool"), GUILayout.Width(40), GUILayout.Height(40)))
                {
                    // Deselects current fields
                    GUI.FocusControl(null);

                    recipeToModify = filteredRecipeList[i];

                    // Populate the ingredients and result item
                    currentIngredients = recipeToModify.ingredients.ToList();
                    resultItem = recipeToModify.result;
                    minimumPlayerLevelRequired = recipeToModify.minLevelRequired;
                    resultAmount = recipeToModify.resultAmount;
                    timeToCraft = recipeToModify.timeToCraft;
                }

                // Delete Button
                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), GUILayout.Width(40), GUILayout.Height(40)))
                {
                    // Show confirmation dialog before deleting
                    bool confirmDelete = EditorUtility.DisplayDialog(
                        "Delete Recipe",
                        "Are you sure you want to delete this recipe?",
                        "Yes", "No"
                    );

                    if (confirmDelete)
                    {
                        // Get the path of the Recipe_SO & remove it
                        string recipePath = AssetDatabase.GetAssetPath(recipesSOList[i]);
                        recipesSOList.RemoveAt(i);
                        AssetDatabase.DeleteAsset(recipePath);
                        AssetDatabase.Refresh();
                        AssetDatabase.Refresh();
                        i--;
                        continue;
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (problematicRecipesFound) EditorGUILayout.HelpBox($"Problematic Recipe(s) have been detected.", MessageType.Error);

        }
        private void RecipeHandling(bool updateRecipe, float rightSectionWidth)
        {
            string title = updateRecipe ? "Update Recipe" : "Create Recipe"; 
            GUILayout.Label(title, bigHeadingStyle);

            timeToCraft = EditorGUILayout.FloatField("Time to Craft (Seconds)", timeToCraft);
            minimumPlayerLevelRequired = EditorGUILayout.IntField("Minimum Player Level Required", minimumPlayerLevelRequired);
            resultAmount = EditorGUILayout.IntField("Result Craft Amount", resultAmount);

            if (timeToCraft < 0) timeToCraft = 0;

            if(minimumPlayerLevelRequired < 0) minimumPlayerLevelRequired = 0;

            if(resultAmount < 1) resultAmount = 1;
            else if (resultAmount >= 99) resultAmount = 99;

            GUILayout.Space(5);

            if(currentIngredients.Count == 0) EditorGUILayout.HelpBox($"\nThis recipe doesn´t include any ingredients.\n", MessageType.Error);

            if (resultItem == null) EditorGUILayout.HelpBox($"\nThis recipe doesn´t have a result item assigned.\n", MessageType.Error);

            GUILayout.Label("Ingredients:", titleStyle);

            // Drop Area for Ingredients
            Rect dropArea = GUILayoutUtility.GetRect(300, 40, GUILayout.ExpandWidth(true));
            GUI.BeginGroup(dropArea, GUI.skin.GetStyle("HelpBox"));
            GUI.Label(new Rect(10, 10, dropArea.width - 20, 20), "Drag & Drop Ingredients here", EditorStyles.boldLabel);
            GUI.EndGroup();

            // Drag & Drop Handling for Ingredients
            if (DragAndDrop.objectReferences.Length > 0 && dropArea.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    Item_SO draggedItem = DragAndDrop.objectReferences[0] as Item_SO;
                    if (draggedItem != null)
                    {
                        string quantityString = EditorGUILayout.TextField("Quantity", "1");
                        int quantity = 1;
                        if (int.TryParse(quantityString, out quantity) && quantity > 0)
                        {
                            // Check if the item already exists in the currentIngredients list
                            var existingIngredient = currentIngredients.Find(ingredient => ingredient.item == draggedItem);
                            // If the item exists, increment its quantity instead of adding it again
                            if (existingIngredient != null) 
                                existingIngredient.quantity += quantity;
                            else 
                                currentIngredients.Add(new Recipe_SO.Ingredient(draggedItem, quantity));
                        }
                        else
                        {
                            // Default to 1 if invalid input
                            var existingIngredient = currentIngredients.Find(ingredient => ingredient.item == draggedItem);
                            if (existingIngredient != null)
                                existingIngredient.quantity += 1;
                            else
                                currentIngredients.Add(new Recipe_SO.Ingredient(draggedItem, 1));
                        }
                    }
                    Event.current.Use();
                }
            }

            GUILayout.Space(10);
            if(currentIngredients.Count > 0)
            {
                ingredientsScrollPosition = GUILayout.BeginScrollView(ingredientsScrollPosition, GUILayout.Height(150));

                GUILayout.BeginHorizontal();
                for (int i = 0; i < currentIngredients.Count; i++)
                {
                    Recipe_SO.Ingredient ingredientEntry = currentIngredients[i];
                    Texture2D icon = AssetPreview.GetAssetPreview(ingredientEntry.item.icon);

                    GUILayout.BeginVertical(GUILayout.Width(50));
                    GUILayout.Label(icon, GUILayout.Width(50), GUILayout.Height(70));
                    GUILayout.Label(ingredientEntry.item.name, EditorStyles.miniLabel);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Quantity", GUILayout.Width(70));

                    // Decrease quantity
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        if (ingredientEntry.quantity > 1)
                            ingredientEntry.quantity -= 1;
                    }

                    string quantityString = ingredientEntry.quantity.ToString();
                    string newQuantityString = EditorGUILayout.TextField("", quantityString, GUILayout.Width(20));

                    // Increment quantity
                    if (GUILayout.Button("+", GUILayout.Width(20)))
                    {
                        ingredientEntry.quantity += 1;
                    }

                    // Check if the quantity has been modified & Update the quantity
                    if (newQuantityString != quantityString && int.TryParse(newQuantityString, out int newQuantity) && newQuantity > 0)
                        ingredientEntry.quantity = newQuantity;

                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        currentIngredients.RemoveAt(i);
                    }

                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();

                GUILayout.EndScrollView();

            }

            GUILayout.Label("Result:", titleStyle);

            // Drop Area for Result Item
            Rect resultDropArea = GUILayoutUtility.GetRect(300, 40, GUILayout.ExpandWidth(true));
            GUI.BeginGroup(resultDropArea, GUI.skin.GetStyle("HelpBox"));
            GUI.Label(new Rect(10, 10, dropArea.width - 20, 20), "Drag & Drop Result here", EditorStyles.boldLabel);
            GUI.EndGroup();

            // Handle Dragging the Result Item
            if (DragAndDrop.objectReferences.Length > 0 && resultDropArea.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    resultItem = DragAndDrop.objectReferences[0] as Item_SO;
                    Event.current.Use();
                }
            }

            if (resultItem != null)
            {
                GUILayout.Label(resultItem.name);
                Texture2D resultIcon = AssetPreview.GetAssetPreview(resultItem.icon);
                GUILayout.Label(resultIcon, GUILayout.Width(90), GUILayout.Height(90));
            }

            GUILayout.Space(10);

            if (GUILayout.Button(title, GUILayout.Width(rightSectionWidth), GUILayout.Height(40)))
            {
                if (resultItem == null || currentIngredients.Count == 0)
                {
                    EditorUtility.DisplayDialog("Invalid Recipe", "Please ensure the recipe has a result item and at least one ingredient.", "OK");
                    return;
                }

                if (updateRecipe)
                {
                    // Update existing recipe
                    recipeToModify.result = resultItem;
                    recipeToModify.ingredients = currentIngredients.ToArray();
                    recipeToModify.timeToCraft = timeToCraft;
                    recipeToModify.minLevelRequired = minimumPlayerLevelRequired;
                    recipeToModify.resultAmount = resultAmount;
                    // Save the Recipe
                    EditorUtility.SetDirty(recipeToModify);
                }
                else
                {
                    // Check for duplicate recipes
                    if (recipesSOList.Any(r => r.result == resultItem))
                    {
                        EditorUtility.DisplayDialog("Duplicate Recipe", "Your recipe has been created, but a recipe for this result item already exists.", "OK");
                    }

                    // Create a new recipe
                    Recipe_SO newRecipe = ScriptableObject.CreateInstance<Recipe_SO>();
                    newRecipe.name = $"{resultItem.name} Recipe"; 
                    newRecipe.result = resultItem;
                    newRecipe.ingredients = currentIngredients.ToArray();
                    newRecipe.timeToCraft = timeToCraft;
                    newRecipe.minLevelRequired = minimumPlayerLevelRequired;
                    newRecipe.resultAmount = resultAmount;

                    // Save
                    string directoryPath = "Assets/Resources/Cowsins Recipes";
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                        AssetDatabase.Refresh();
                    }

                    string path = $"{directoryPath}/{resultItem.name} Recipe.asset";
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    AssetDatabase.CreateAsset(newRecipe, path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh(); 

                    // Update the local list
                    recipesSOList.Add(newRecipe);
                }

                EditorUtility.DisplayDialog("Recipe Saved", "The recipe has been successfully saved.", "OK");
                // Deselects current fields
                GUI.FocusControl(null);
                // Clear selections for next recipe
                currentIngredients.Clear();
                resultItem = null;
                minimumPlayerLevelRequired = 0;
                resultAmount = 1;
                timeToCraft = 0;
            }
        }

        private void TutorialsTab()
        {
            GUILayout.Space(10);
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
                if (GUILayout.Button("[RELATED DOCUMENTATION] CREATING & UPDATING RECIPES", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://cowsinss-organization.gitbook.io/inventory-pro-add-on-documentation/how-to-use-and-guides/creating-updating-and-deleting-recipes");
                }
                GUILayout.Space(5);
                GUILayout.Label("The Crafting system requires recipes. Recipes define the outcome of Items based on their ingredients. " +
                    "It involves a transaction of Item_SOs (resources/ingredients) in exchange for other Items (results). " +
                    "Fortunately, managing new recipes in FPS Engine with the Inventory Pro Add-On is very simple!", wrapStyle);
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

        #region UTILITIES
        private void LoadScriptableObjects()
        {
            itemSOList = AssetDatabase.FindAssets("t:Item_SO")
                                      .Select(guid => AssetDatabase.LoadAssetAtPath<Item_SO>(AssetDatabase.GUIDToAssetPath(guid)))
                                      .Where(item => item != null)
                                      .ToList();

            recipesSOList = AssetDatabase.FindAssets("t:Recipe_SO")
                                         .Select(guid => AssetDatabase.LoadAssetAtPath<Recipe_SO>(AssetDatabase.GUIDToAssetPath(guid)))
                                         .Where(recipe => recipe != null)
                                         .ToList();
        }

        private void InitializeStyles()
        {
            titleStyle = CowsinsEditorWindowUtilities.TitleStyle();
            bigHeadingStyle = CowsinsEditorWindowUtilities.BigHeadingStyle();
        }
        #endregion
    }

}
#endif
