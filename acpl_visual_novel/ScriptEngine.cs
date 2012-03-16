using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using acpl.ScriptAssets;

namespace acpl.ScriptEngine
{
    public class Script
    {
        private Boolean frozen = false;
        private Event currentEvent;
        private Event[] events;
        private KeyStore keys = new KeyStore();
        private Boolean done = false;
        private GameEngine.Core engine;
        
        public Script(String scriptFileName, GameEngine.Core engine)
        {
            this.engine = engine;
            int eventCount = 0;
            String choiceRegex = "^" + Regex.Escape("*") + "(.*)" + Regex.Escape("*");
            String eventRegex = "^([0-9]+):(.*)/(.*)/";
            String dialogRegex = "^" + Regex.Escape(">") + "(.*?):(.*)" + Regex.Escape("<");
            String commandRegex = Regex.Escape("[") + "(.*?)" + Regex.Escape("]");
            String conditionRegex = Regex.Escape("(") + "(.*)" + Regex.Escape(")");
            String lineCommandRegex = "^(" + commandRegex + ")+[\t ]*(" + conditionRegex + ")*";
            String pathRegex = "(.+)" + Regex.Escape("->") + "[ \t]*([0-9]+)";
            List<String> actors = new List<String>();
            List<String> locations = new List<String>();
            MatchCollection matches;
            MatchCollection commands;
            MatchCollection path;
            try
            {
                events = new Event[1024];

                for (int i = 0; i < 1024; i++)
                    events[i] = new Event();

                using (StreamReader sr = new StreamReader(scriptFileName))
                {
                    String line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if ((matches = Regex.Matches(line, lineCommandRegex)).Count > 0)
                        {
                            Command element = new Command();
                            Debug.WriteLine("Found command line: ");
                            foreach (Match match in matches)
                            {
                                element.raw = match.Groups[2].Value;
                                if(match.Groups[3].Value != "")
                                    element.condition = ComplexCondition.Parse(match.Groups[3].Value);
                                
                                String[] tokens = match.Groups[2].Value.Split(' ');

                                if (tokens.Length >= 2)
                                {
                                    switch (tokens[0])
                                    {
                                        case "set":
                                            element.type = CommandType.SET;
                                            element.asset.type = AssetType.KEY;
                                            break;
                                        case "change":
                                            element.type = CommandType.CHANGE;
                                            element.asset.type = AssetType.ACTOR;
                                            if (!actors.Contains((tokens[1].Split(':'))[0]))
                                                actors.Add((tokens[1].Split(':'))[0]);
                                            break;
                                        case "move":
                                            element.type = CommandType.MOVE;
                                            element.asset.type = AssetType.LOCATION;
                                            if (!locations.Contains((tokens[1].Split(':'))[0]))
                                                locations.Add((tokens[1].Split(':'))[0]);
                                            break;
                                        case "add":
                                            element.type = CommandType.ADD;
                                            element.asset.type = AssetType.ACTOR;
                                            if (!actors.Contains((tokens[1].Split(':'))[0]))
                                                actors.Add((tokens[1].Split(':'))[0]);
                                            break;
                                        case "reset":
                                            element.type = CommandType.RESET;
                                            element.asset.type = AssetType.KEY;
                                            break;
                                    }
                                    element.asset.name = tokens[1];
                                    events[eventCount].orderedElements.Add(element);
                                }
                                Debug.WriteLine("\tCOMMAND:   " + match.Groups[2].Value);
                                Debug.WriteLine("\tCONDITION: " + match.Groups[3].Value);
                            }
                        }
                        else if ((matches = Regex.Matches(line, eventRegex)).Count > 0)
                        {
                            foreach (Match match in matches)
                            {
                                eventCount = Int32.Parse(match.Groups[1].Value);
                                Debug.WriteLine("Parsing event " + eventCount);
                                events[eventCount].text = match.Groups[2].Value + " at " + match.Groups[3].Value;
                            }
                        }
                        else if ((matches = Regex.Matches(line, choiceRegex)).Count > 0)
                        {
                            Choice choice = new Choice();
                            foreach (Match match in matches)
                            {
                                if ((path = Regex.Matches(match.Groups[1].Value, pathRegex)).Count > 0)
                                {
                                    choice.text = path[0].Groups[1].Value;
                                    choice.nextEvent = events[Int32.Parse(path[0].Groups[2].Value)];
                                }

                                if ((commands = Regex.Matches(line, commandRegex)).Count > 0)
                                {
                                    foreach (Match command in commands)
                                    {
                                        String[] tokens = command.Groups[1].Value.Split(' ');

                                        if (tokens.Length >= 2)
                                        {
                                            Command oCommand = new Command();
                                            oCommand.raw = command.Groups[1].Value;
                                            switch (tokens[0])
                                            {
                                                case "set":
                                                    oCommand.type = CommandType.SET;
                                                    oCommand.asset.type = AssetType.KEY;
                                                    break;
                                            }
                                            oCommand.asset.name = tokens[1];
                                            choice.commands.Add(oCommand);
                                        }
                                    }
                                }

                                Match condition = Regex.Match(line, conditionRegex);
                                if (condition.Groups[1].Value != "")
                                {
                                    Debug.WriteLine("CONDITION: " + condition.Groups[1].Value);
                                    Condition oCondition = ComplexCondition.Parse(condition.Groups[1].Value);
                                    choice.condition = oCondition;
                                }
                            }
                            events[eventCount].choices.Add(choice);
                        }
                        else if ((matches = Regex.Matches(line, dialogRegex)).Count > 0)
                        {
                            Dialog dialog = new Dialog();
                            foreach (Match match in matches)
                            {
                                dialog.actor.name = match.Groups[1].Value;
                                dialog.text = match.Groups[2].Value;

                                if (!actors.Contains(dialog.actor.name))
                                    actors.Add(dialog.actor.name);

                                Match condition = Regex.Match(line, conditionRegex);
                                if (condition.Groups[1].Value != "")
                                {
                                    Debug.WriteLine("CONDITION: " + condition.Groups[1].Value);
                                    Condition oCondition = ComplexCondition.Parse(condition.Groups[1].Value);
                                    dialog.condition = oCondition;
                                }
                            }
                            events[eventCount].orderedElements.Add(dialog);
                            events[eventCount].dialogs.Add(dialog);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unable to read file!");
                Debug.WriteLine(e.Message);
            }

            currentEvent = events[0];

            foreach (String location in locations)
                engine.addLocation(location.ToLower());

            foreach (String actor in actors)
                engine.addActor(actor.ToLower());
        }

        public EventState getCurrentEventState()
        {
            return currentEvent.GetState();
        }

        public void Freeze()
        {
            frozen = true;
        }

        public void Unfreeze()
        {
            frozen = false;
        }

        public Boolean isDone()
        {
            return done;
        }

        public void NextItem()
        {
            if (!frozen)
            {
                switch (currentEvent.GetState())
                {
                    case EventState.START:
                        NextEvent();
                        break;
                    case EventState.DIALOG:
                        ScriptElement nextElement = currentEvent.NextElement();

                        if (nextElement == null)
                        {
                            NextItem();
                            return;
                        }

                        switch (nextElement.etype)
                        {
                            case ElementType.COMMAND:
                                Command command = (Command)nextElement;
                                switch (command.type)
                                {
                                    case CommandType.RESET:
                                        if (command.asset.name == "all")
                                            this.keys.Clear();
                                        break;
                                    case CommandType.SET:
                                        if (command.asset.type == AssetType.KEY)
                                            this.keys.Activate(command.asset.name);
                                        break;
                                    default:
                                        engine.issueCommand(command.raw);
                                        break;
                                }
                                NextItem();
                                return;
                            case ElementType.DIALOG:
                                Dialog dialog = (Dialog)nextElement;
                                engine.addTextLine(dialog.actor.name + ": " + dialog.text);
                                break;
                        }
                        break;
                    case EventState.CHOICES:
                        engine.presentWithChoices(currentEvent.GetChoices());
                        Freeze();
                        break;
                    case EventState.END:
                        NextEvent();
                        break;
                }
            }
        }

        private void NextEvent()
        {
            if (currentEvent.orderedElements.Count == 0)
            {
                done = true;
                return;
            }

            engine.clear();

            String eventText = currentEvent.Start();

            String line = "";
            for (int i = 0; i < eventText.Length; i++)
                line += "-";

            engine.addTextLine(eventText);
            engine.addTextLine(line);
            currentEvent.Validate(keys);
            NextItem();
        }

        public void MakeChoice(Choice choice)
        {
            Choice selectedChoice = choice;
            if (selectedChoice.condition == null || selectedChoice.condition.Evaluate(keys))
            {
                foreach (Command command in selectedChoice.commands)
                {
                    engine.issueCommand(command.raw);

                    switch (command.type)
                    {
                        case CommandType.RESET:
                            if (command.asset.name == "all")
                                this.keys.Clear();
                            break;
                        case CommandType.SET:
                            if (command.asset.type == AssetType.KEY)
                                this.keys.Activate(command.asset.name);
                            break;
                    }
                }

                currentEvent = selectedChoice.nextEvent;
                Unfreeze();
                NextItem();
            }
        }
    }
}
