/// <summary>
/// This script is responsible for placing wires on the grid
/// </summary>
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WirePlacement : MonoBehaviour
{
    public GameObject StraightWire;
    public GameObject CornerWire;
    private GameObject lastWire;
    public Material CompletedConnection; //Just using one material for now. Can change to different ones based on the building
    private Material selectedMaterial;
    private List<int> tilesPlaced = new List<int>();
    private List<List<int>> wiresPlaced = new List<List<int>>(); //This is the list of saved wires so they can be removed if the building is removed
    private List<int> buildingTiles = new List<int>(); //This is the list of building tiles that don't have a wire attached to them
    public List<int> connectedBuildings = new List<int>(); //Buildings that have been connected to the goal
    private int startingTile = -1;
    private int lastTile = -1;
    private int secondLastTile = -1;
    private int gridsize;
    private TileTypes goal = TileTypes.Goal;
    //I have an intermediary variable so that I only need to change it in one place if the name of the tile is changed in the goal branch

    //Singleton pattern
    public static WirePlacement Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        gridsize = GridManager.Instance.gridSize;
    }
    // Update is called once per frame
    void Update()
    {
        if (InventoryManagement.instance.deleteMode.isOn) //If delete mode is on, don't place wires
        {
            return;
        }
        //This checks if the player has just placed a building and is now trying to place wires
        if (BuildingPlacing.WiresPlacing)
        {
            WirePlacingLogic();
        }
        else if (buildingTiles.Contains(MouseManager.gridPosition) && Input.GetMouseButtonDown(0))
        {
            BuildingPlacing.WiresPlacing = true;
            startingTile = MouseManager.gridPosition;
        }
    }

    /// <summary>
    ///   Check if a building is connected to the goal tile
    /// </summary>
    /// <param name="gridPosition">Building to check</param>
    /// <returns></returns>
    public bool isTileConnected(int gridPosition)
    {
        if (connectedBuildings.Contains(gridPosition))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    ///    The logic for placing wires
    ///    The player can place wires on empty tiles and remove wires by dragging over tiles that already have wires
    /// </summary>
    private void WirePlacingLogic()
    {
        //The player will only place wires for as long as they are holding down left click and dragging
        //It will also stop placing wires if the player is not hovering over a tile
        if (Input.GetMouseButton(0) && MouseManager.isHovering)
        {
            //If this is the first tile set starting tile to the current tile
            if (startingTile == -1)
            {
                startingTile = MouseManager.gridPosition;
                buildingTiles.Add(startingTile);
            }
            lastTile = tilesPlaced.Count > 0 ? tilesPlaced[tilesPlaced.Count - 1] : startingTile; //Either gets the last tile or the starting tile
            secondLastTile = tilesPlaced.Count > 1 ? tilesPlaced[tilesPlaced.Count - 2] : startingTile; //Either gets the second last tile or the starting tile
            //If the player drags through a tile that is empty, they can place a wire
            if (GridManager.IsTileEmpty(MouseManager.gridPosition))
            {
                int rotation = 0;
                //If the player is trying to place a wire diagonally or across multiple tiles, remove the wires placed so far
                //This uses abs and the grid size to check if the difference in both directions is over 0
                if (illegalMoveCheck())
                {
                    foreach (int tile in tilesPlaced)
                    {
                        RemoveWire(tile);
                    }
                    resetTileList();
                    return;
                }
                //Checks if the last wire needs to become a corner wire
                PlacingCornerWire();
                //Checking the rotation of the current wire
                if (MouseManager.gridPosition / gridsize == lastTile / gridsize)
                {
                    rotation = 90;
                }
                else
                {
                    rotation = 0;
                }
                //Place the wire on the current tile
                PlaceWire(MouseManager.gridPosition, MouseManager.Instance.playerX, MouseManager.Instance.playerZ, rotation, StraightWire);
                //Adding the placed tile to the list
                tilesPlaced.Add(MouseManager.gridPosition);
            }
            //If they drag over the goal tile, they lock in the wire
            else if (GridManager.Instance.tileStates[MouseManager.gridPosition] == goal && !illegalMoveCheck())
            {
                //Checks if the last wire needs to become a corner wire
                PlacingCornerWire();
                //Can swap out the material based on the building when we add the materials
                switch (GridManager.Instance.tileStates[startingTile])
                {
                    case TileTypes.SolarPanels:
                        selectedMaterial = CompletedConnection;
                        break;
                    case TileTypes.Windmills:
                        selectedMaterial = CompletedConnection;
                        break;
                    //case TileTypes. whatever we use for water
                    //    selectedMaterial = CompletedConnection;
                    //    break;
                    default:
                        Debug.Log("The source of the wire was not a valid building type.");
                        //If this is reached something is broken
                        selectedMaterial = CompletedConnection;
                        break;
                }
                //Change the colour of the wire to the target material
                //I need to invoke it or the colour change will happen before the last corner wire is placed for some reason
                Invoke("EndWirePlace", 0f);
            }
            //If the player drags over a tile that already has a wire from this list, they can remove everything placed since that tile was placed
            //If it is the source building it removes everything but lets the player keep placing wires
            else if ((tilesPlaced.Contains(MouseManager.gridPosition) || startingTile == MouseManager.gridPosition) && lastTile != MouseManager.gridPosition)
            {
                //While there are tiles left and it hasn't gotten to the tile the player is currently hovering over
                while (tilesPlaced.Count > 0 && tilesPlaced[tilesPlaced.Count - 1] != MouseManager.gridPosition)
                {
                    //Removed the wires from the tile and remove the tile from the list
                    RemoveWire(tilesPlaced[tilesPlaced.Count - 1]);
                    tilesPlaced.RemoveAt(tilesPlaced.Count - 1);
                    lastTile = tilesPlaced.Count > 0 ? tilesPlaced[tilesPlaced.Count - 1] : startingTile; //Resets what the last tile and second last tile are every time one is deleted
                    secondLastTile = tilesPlaced.Count > 1 ? tilesPlaced[tilesPlaced.Count - 2] : startingTile;
                    //If this removes all wires then stop placing wires unless this is the starting tile
                    if (tilesPlaced.Count == 0 && lastTile != startingTile)
                    {
                        resetTileList();
                    }
                }
            }
        }
        else if (!Input.GetMouseButton(0)) //If the player releases the left click
        {
            //If the player releases the left click before reaching the goal tile, remove the wires and refund the building
            foreach (int tile in tilesPlaced)
            {
                RemoveWire(tile);
            }
            resetTileList();
        }
    }

    private void EndWirePlace()
    {
        foreach (int tile in tilesPlaced)
        {
            changeColour(GridCreator.tiles[tile].transform.GetChild(0).gameObject, CompletedConnection);
        }
        //Add the starting spot to the start of the list of placed wires
        tilesPlaced.Insert(0, startingTile);
        //Add the starting tile to the list of connected buildings
        connectedBuildings.Add(startingTile);
        wiresPlaced.Add(new List<int>(tilesPlaced)); //Add the list of placed wires to the list of all placed wires
        buildingTiles.Remove(startingTile); //Remove the starting tile from the list of building tiles wihtout wires
        resetTileList();
    }
    /// <summary>
    /// This is used for placing a corner wire on the last tile if needed
    /// It is a separate function for code readability and because it is only used in two places
    /// </summary>
    /// <param name="currentTile">The tile that the player is currently hovering over. By default this is grid position</param>
    private void PlacingCornerWire(int currentTile = -1)
    {
        if (currentTile == -1)
        {
            currentTile = MouseManager.gridPosition;
        }
        int rotation = 0;
        //This checks if the player has placed a wire on a different axis as the last two tiles
        if ((lastTile / gridsize != secondLastTile / gridsize || lastTile / gridsize != currentTile / gridsize) &&
            (lastTile % gridsize != secondLastTile % gridsize || lastTile % gridsize != currentTile % gridsize))
        {
            // Determine the direction of movement from the second last tile to the last tile and from the last tile to the current tile
            bool isMovingRightOrUpSecondLastToLast = secondLastTile < lastTile;
            bool isMovingRightOrUpLastToCurrent = lastTile < currentTile;

            // Determine the rotation of the corner wire. I just put different rotations in until all directions worked.
            if (currentTile / gridsize == lastTile / gridsize)
            {
                if (isMovingRightOrUpSecondLastToLast)
                {
                    if (isMovingRightOrUpLastToCurrent)
                    {
                        rotation = 270;
                    }
                    else
                    {
                        rotation = 180;
                    }
                }
                else
                {
                    if (isMovingRightOrUpLastToCurrent)
                    {
                        rotation = 0;
                    }
                    else
                    {
                        rotation = 90;
                    }
                }
            }
            else
            {
                if (isMovingRightOrUpSecondLastToLast)
                {
                    if (isMovingRightOrUpLastToCurrent)
                    {
                        rotation = 90;
                    }
                    else
                    {
                        rotation = 180;
                    }
                }
                else
                {
                    if (isMovingRightOrUpLastToCurrent)
                    {
                        rotation = 0;
                    }
                    else
                    {
                        rotation = 270;
                    }
                }
            }
            lastWire = CornerWire; //Set the last wire to a corner wire
        }
        else
        {
            //If the player is placing a wire straight
            //This is so that when you backtrack over a corner wire it will properly straight if it needs to
            if (lastTile / gridsize == secondLastTile / gridsize)
            {
                rotation = 90;
            }
            else
            {
                rotation = 0;
            }
            lastWire = StraightWire;
        }
        //Place the wire on the last tile if it is not the starting tile
        if (lastTile != startingTile)
        {
            PlaceWire(lastTile, lastTile / gridsize, lastTile % gridsize, rotation, lastWire);
        }
    }

    /// <summary>
    ///   Check if the player is trying to make an illegal move
    ///   If the move is diagonal but could be legal with a tile placed in an empty square between them it will do that and return false
    /// </summary>
    /// <returns>Returns true if move is illegal</returns>
    private bool illegalMoveCheck()
    {
        if ((Math.Abs((MouseManager.gridPosition % gridsize) - (lastTile % gridsize)) > 1) ||
        (Math.Abs((MouseManager.gridPosition / gridsize) - (lastTile / gridsize)) > 1)) //If it is a movement of more than one tile
        {
            //CONSIDER: I could probably do the same thing as the diagonal movement for this. Should I?
            return true; //Illegal move
        }
        else if ((Math.Abs((MouseManager.gridPosition % gridsize) - (lastTile % gridsize)) > 0) &&
            (Math.Abs((MouseManager.gridPosition / gridsize) - (lastTile / gridsize)) > 0)) //If a diagonal movement is made
        {
            // Calculate the positions of the two squares in between the diagonal move
            // I asked an AI for the correct calculation to figure out these two squares because it is 1am and my brain hurt trying to figure it out
            int square1 = MouseManager.gridPosition % gridsize > lastTile % gridsize ? lastTile + 1 : lastTile - 1;
            int square2 = MouseManager.gridPosition / gridsize > lastTile / gridsize ? lastTile + gridsize : lastTile - gridsize;

            // Check if either of the squares is empty
            if (GridManager.IsTileEmpty(square1) || GridManager.IsTileEmpty(square2))
            {
                int emptySquare;
                // Place a wire on the empty square
                if (GridManager.IsTileEmpty(square1))
                {
                    emptySquare = square1;
                }
                else
                {
                    emptySquare = square2;
                }
                PlacingCornerWire(emptySquare); //Because there is suddenly a new wire that might be a corner
                PlaceWire(emptySquare, emptySquare / gridsize, emptySquare % gridsize, 0, StraightWire);

                // Update lastTile, secondLastTile, and tilesPlaced
                secondLastTile = lastTile;
                lastTile = emptySquare;
                tilesPlaced.Add(emptySquare);

                return false; //Legal move with the tile placed in the empty square
            }
            else
            {
                return true; //Illegal move
            }
        }
        else
        {
            return false; //Legal move
        }
    }

    /// <summary>
    ///    Reset the list of placed tiles and stop placing wires
    /// </summary>
    private void resetTileList()
    {
        BuildingPlacing.WiresPlacing = false;
        //Clear the list of placed tiles
        tilesPlaced.Clear();
        startingTile = -1;
        lastTile = -1;
        secondLastTile = -1;
    }

    /// <summary>
    ///     Place a wire on the inputted tile
    /// </summary>
    /// <param name="position"> Position of the tile to place the wire on </param>
    /// <param name="wireX"> X position of the wire </param>
    /// <param name="wireZ"> Z position of the wire </param>
    /// <param name="rotation"> Rotation of the wire </param>
    /// <param name="wireType"> Type of wire to place </param>
    private void PlaceWire(int position, int wireX, int wireZ, int rotation, GameObject wireType)
    {
        //Clear any wire that already exists on the tile
        RemoveWire(position);
        //Instantiate the wire at the position of the tile
        GameObject temp = Instantiate(wireType, GridManager.CalculatePos(wireX, wireZ), Quaternion.Euler(0, rotation, 0));
        //set the parent of the wire to the tile
        temp.transform.SetParent(GridCreator.tiles[position].transform);
        GridManager.Instance.tileStates[position] = TileTypes.Wires;
    }

    /// <summary>
    ///   Remove a wire from the tile at the given position
    /// </summary>
    /// <param name="position"> Position of the tile to remove the wire from </param>
    private void RemoveWire(int position)
    {
        //If the tile has a wire, remove it
        if (GridCreator.tiles[position].transform.childCount > 0)
        {
            Destroy(GridCreator.tiles[position].transform.GetChild(0).gameObject);
            GridManager.Instance.tileStates[position] = TileTypes.None;
        }
    }

    /// <summary>
    ///    Change the colour of the wire to the target material
    /// </summary>
    /// <param name="wire"> Game Object to be changed </param>
    /// <param name="targetMaterial"> Material to change it to </param>
    private void changeColour(GameObject wire, Material targetMaterial)
    {
        //change material of the all components in the wire to the target material
        foreach (Transform child in wire.transform)
        {
            child.gameObject.GetComponent<MeshRenderer>().material = targetMaterial;
        }
    }

    /// <summary>
    /// Deletes a full wire when given a building tile starting location
    /// </summary>
    /// <param name="buildingTile">Building that is being deleted</param>
    public void RemoveFullWire(int buildingTile)
    {
        //Remove the building from the list of connected buildings
        connectedBuildings.Remove(buildingTile);
        buildingTiles.Remove(buildingTile);
        foreach (List<int> wire in wiresPlaced)
        {
            if (buildingTile == wire[0]) //If the first tile is the same as the inputted building tile
            {
                foreach (int tile in wire)
                {
                    RemoveWire(tile);
                }
            }
        }
    }
}
