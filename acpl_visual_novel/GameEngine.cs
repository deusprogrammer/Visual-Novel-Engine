using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

using acpl.Assets;

namespace acpl.GameEngine
{
    public enum GameState {TITLE, PLAYING, START, END}

    public class Command
    {
        public String name, asset, subAsset, side;

        public Command()
        {
        }

        public Command(String commandString)
        {
            String commandRegex = "(set|add|change|move|change_textbox)[ \t]*([a-zA-Z0-9]+):([a-zA-Z0-9]+):*(right|left)*";

            Match match;
            if ((match = Regex.Match(commandString, commandRegex)).Groups.Count > 3)
            {
                name = match.Groups[1].Value;
                asset = match.Groups[2].Value;
                subAsset = match.Groups[3].Value;
                side = match.Groups[4].Value;

                Debug.WriteLine("COMMAND:  " + name);
                Debug.WriteLine("ASSET:    " + asset);
                Debug.WriteLine("SUBASSET: " + subAsset);
                Debug.WriteLine("SIDE:     " + side);
            }
        }
    }

    public class Core
    {
        protected Boolean changed = false;
        private ActorAsset leftCharacterAsset, rightCharacterAsset;
        private Texture2D leftCharacter, rightCharacter;
        private Texture2D background;
        private Texture2D textbox;
        private ScriptAssets.Choice selectedChoice;
        private List<acpl.ScriptAssets.Choice> lastChoices;
        private String text = "";
        private String overflowText = "";
        private Boolean shutdown = false;
        private GameState state = GameState.START;
        private Boolean mouseLatch = false;
        private Boolean keyboardLatch = false;
        private int textHeight = 0;
        private AssetStore actors = new AssetStore();
        private AssetStore locations = new AssetStore();
        private AssetStore textboxes = new AssetStore();
        private ScriptEngine.Script script;
        static Core instance = new Core();
        static private SpriteFont font;
        static private int width, height;
        static private SpriteBatch foregroundBatch, backgroundBatch, textBoxBatch;
        static private GraphicsDevice graphicsDevice;

        private Core()
        {
        }

        static public Core getInstance()
        {
            return instance;
        }

        public Boolean getShutdownStatus()
        {
            return shutdown;
        }

        static public void setResolution(int width, int height)
        {
            Core.width = width;
            Core.height = height;
        }

        public void loadScript(String scriptFile)
        {
            script = new ScriptEngine.Script(scriptFile, this);
        }

        static public void setGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            Core.graphicsDevice = graphicsDevice;
            foregroundBatch = new SpriteBatch(graphicsDevice);
            backgroundBatch = new SpriteBatch(graphicsDevice);
            textBoxBatch = new SpriteBatch(graphicsDevice);
        }

        static public void setFont(SpriteFont font)
        {
            Core.font = font;
        }

        public void start()
        {
            loadTextBoxes();
            issueCommand("change_textbox textbox:default");

            script.NextItem();
            state = GameState.PLAYING;
        }

        public GameState getState()
        {
            return state;
        }

        public void Draw()
        {
            int adjustedHeight = (int)(200 * (4.0 / 6.0));
            int adjustedWidth = (int)(200 * (6.0 / 4.0));

            graphicsDevice.Clear(Color.Black);

            backgroundBatch.Begin(SpriteSortMode.BackToFront, BlendState.Opaque);

            if (background != null)
                backgroundBatch.Draw(background, new Rectangle(0, 0, width, height), Color.White);

            backgroundBatch.End();

            foregroundBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);
            if (leftCharacter != null)
                foregroundBatch.Draw(leftCharacter, new Rectangle(0, (height - 200) - adjustedHeight, adjustedWidth, adjustedHeight), Color.White);
            if (rightCharacter != null)
                foregroundBatch.Draw(rightCharacter, new Rectangle(width - adjustedWidth, (height - 200) - adjustedHeight, adjustedWidth, adjustedHeight), Color.White);
            if (textbox != null)
                foregroundBatch.Draw(textbox, new Rectangle(0, height - 200, 800, 200), Color.White);
            foregroundBatch.End();

            //Draw text depending on current game state.
            textBoxBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);
            switch(script.getCurrentEventState()) {
                case ScriptAssets.EventState.DIALOG:
                    String dialogString = text + "(Click or hit enter to continue)";
                    textBoxBatch.DrawString(font, dialogString, new Vector2(10, (height - 190)), Color.White);
                    break;
                case ScriptAssets.EventState.END:
                    int i = 1;
                    String choicesString = "What do you do?\n";
                    foreach (acpl.ScriptAssets.Choice choice in lastChoices)
                    {
                        choicesString += (i++) + ": " + choice.text + "\n";
                    }
                    textBoxBatch.DrawString(font, choicesString, new Vector2(10, (height - 190)), Color.White);
                    break;
            }
            textBoxBatch.End();
        }

        public Boolean hasChanged()
        {
            Boolean temp = changed;
            changed = false;

            return temp;
        }

        public void addActor(String name)
        {
            actors.Add(new ActorAsset(name, graphicsDevice));
        }

        public void addLocation(String name)
        {
            locations.Add(new LocationAsset(name, graphicsDevice));
        }

        public void loadTextBoxes()
        {
            textboxes.Add(new GraphicalAsset("textbox", graphicsDevice));
        }

        public void clearText()
        {
            text = "";
            textHeight = 0;
        }

        public void addTextLine(String text)
        {
            changed = true;
            String newText = "";
            Boolean overflow = false;
            int lineLength = 0;
            int maxWidth = width - 150;
            int maxHeight = 200 - 20;

            if (textHeight + (int)font.MeasureString(text).Y > 200 - 20)
            {
                if (overflowText.Length == 0)
                    this.text += "(Continued)-->";
                overflowText += text;
                return;
            }

            textHeight += (int)font.MeasureString(text).Y;

            foreach (String word in text.Split(' '))
            {
                if (lineLength + (int)font.MeasureString(word).X <= maxWidth)
                {
                    lineLength += (int)font.MeasureString(word).X;
                    if (overflow)
                        this.overflowText += word + " ";
                    else
                        newText += word + " ";
                }
                else
                {
                    lineLength = (int)font.MeasureString(word).X;
                    if (textHeight + (int)font.MeasureString(word).Y > maxHeight)
                    {
                        overflow = true;
                    }

                    textHeight += (int)font.MeasureString(word).Y;

                    if (overflow)
                        this.overflowText += "\n" + word + " ";
                    else
                        newText += "\n" + word + " ";
                }
            }

            this.text += newText + "\n";
        }

        public void clearGraphics()
        {
            leftCharacter = null;
            rightCharacter = null;
            background = null;
        }

        public void clear()
        {
            clearGraphics();
            clearText();
        }

        public void presentWithChoices(List<acpl.ScriptAssets.Choice> choices)
        {
            this.lastChoices = choices;
        }

        public void checkForInput()
        {
            Boolean enterKeyPressed = false;
            Boolean mouseClicked = false;
            Keys lastKeyPressed = Keys.None;
            int selection = 0;

            if (Mouse.GetState().LeftButton == ButtonState.Pressed && !mouseLatch)
            {
                mouseLatch = true;
                mouseClicked = true;
            }
            else if (Mouse.GetState().LeftButton == ButtonState.Released)
            {
                mouseLatch = false;
            }

            if (Keyboard.GetState().IsKeyDown(Keys.Enter) && !keyboardLatch)
            {
                keyboardLatch = true;
                enterKeyPressed = true;
            }
            else if (Keyboard.GetState().IsKeyUp(Keys.Enter))
            {
                keyboardLatch = false;
            }

            Keys[] keys = Keyboard.GetState().GetPressedKeys();
            foreach (Keys key in keys)
                lastKeyPressed = key;

            if (mouseClicked)
            {
                mouseClicked = false;
                script.NextItem();
            }
            else if (enterKeyPressed)
            {
                enterKeyPressed = false;
                script.NextItem();
            }
            else if (lastKeyPressed != Keys.None)
            {
                switch (lastKeyPressed)
                {
                    case Keys.NumPad1:
                    case Keys.D1:
                        selection = 0;
                        break;
                    case Keys.NumPad2:
                    case Keys.D2:
                        selection = 1;
                        break;
                    case Keys.NumPad3:
                    case Keys.D3:
                        selection = 2;
                        break;
                    case Keys.NumPad4:
                    case Keys.D4:
                        selection = 3;
                        break;
                    case Keys.NumPad5:
                    case Keys.D5:
                        selection = 4;
                        break;
                    case Keys.NumPad6:
                    case Keys.D6:
                        selection = 5;
                        break;
                    case Keys.NumPad7:
                    case Keys.D7:
                        selection = 6;
                        break;
                    case Keys.NumPad8:
                    case Keys.D8:
                        selection = 7;
                        break;
                    case Keys.NumPad9:
                    case Keys.D9:
                        selection = 8;
                        break;
                    default:
                        selection = -1;
                        break;
                }

                if (selection != -1 && selection < lastChoices.Count && script.getCurrentEventState() == ScriptAssets.EventState.END)
                {
                    selectedChoice = lastChoices[selection];
                    lastKeyPressed = Keys.None;
                    script.MakeChoice(selectedChoice);

                    if (script.isDone())
                    {
                        state = GameState.END;
                        shutdown = true;
                    }
                }
            }
        }

        public void issueCommand(String commandString)
        {
            Command command = new Command(commandString);
            changed = true;

            switch (command.name)
            {
                case "change_textbox":
                    GraphicalAsset textboxAsset = (GraphicalAsset)textboxes.Find(command.asset);
                    if(textboxAsset != null)
                        textbox = textboxAsset.getSubAsset(command.subAsset);
                    break;
                case "add":
                    if (command.side == "left")
                    {
                        leftCharacterAsset = (ActorAsset)actors.Find(command.asset);
                        if (leftCharacterAsset != null)
                            leftCharacter = leftCharacterAsset.getSubAsset(command.subAsset);
                    }
                    else if (command.side == "right")
                    {
                        rightCharacterAsset = (ActorAsset)actors.Find(command.asset);
                        if (rightCharacterAsset != null)
                            rightCharacter = rightCharacterAsset.getSubAsset(command.subAsset);
                    }
                    break;
                case "change":
                    if (leftCharacterAsset.name == command.asset)
                    {
                        leftCharacterAsset = (ActorAsset)actors.Find(command.asset);
                        if (leftCharacterAsset != null)
                            leftCharacter = leftCharacterAsset.getSubAsset(command.subAsset);
                    }
                    else if (rightCharacterAsset.name == command.asset)
                    {
                        rightCharacterAsset = (ActorAsset)actors.Find(command.asset);
                        if (rightCharacterAsset != null)
                            rightCharacter = rightCharacterAsset.getSubAsset(command.subAsset);
                    }
                    else
                    {
                        if (command.side == "left")
                        {
                            leftCharacterAsset = (ActorAsset)actors.Find(command.asset);
                            if (leftCharacterAsset != null)
                                leftCharacter = leftCharacterAsset.getSubAsset(command.subAsset);
                        }
                        else if (command.side == "right")
                        {
                            rightCharacterAsset = (ActorAsset)actors.Find(command.asset);
                            if(rightCharacterAsset != null)
                                rightCharacter = rightCharacterAsset.getSubAsset(command.subAsset);
                        }
                    }
                    break;
                case "move":
                    LocationAsset backgroundAsset = (LocationAsset)locations.Find(command.asset);
                    if(backgroundAsset != null)
                        background = backgroundAsset.getSubAsset(command.subAsset);
                    break;
                default:
                    changed = false;
                    break;
            }
        }
    }
}
