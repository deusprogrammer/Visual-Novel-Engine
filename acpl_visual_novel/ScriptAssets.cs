using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace acpl.ScriptAssets
{
    public enum CommandType { SET, CHANGE, MOVE, ADD, RESET }
    public enum AssetType { ACTOR, CONDITION, KEY, LOCATION }
    public enum ElementType { DIALOG, COMMAND, ELEMENT_TYPE }

    public class ScriptElement
    {
        public ElementType etype = ElementType.ELEMENT_TYPE;
        public Condition condition;
    }

    public class Command : ScriptElement
    {
        public CommandType type;
        public Asset asset;
        public String raw;
        public Command()
        {
            etype = ElementType.COMMAND;
            asset = new Asset();
        }
    }

    public class Dialog : ScriptElement
    {
        public Actor actor;
        public String text;
        public List<Command> commands = new List<Command>();
        public Dialog()
        {
            etype = ElementType.DIALOG;
            actor = new Actor();
        }
    }

    public class Location
    {
        public String description;
    }

    public class Asset
    {
        public AssetType type;
        public String name;
    }

    enum ParserState { FOUND, SEARCHING }

    public class BooleanExpression
    {
        public static void Parse(String str)
        {
            ParserState state = ParserState.SEARCHING;
            Stack<String> opStack = new Stack<String>();
            Stack<String> pfStack = new Stack<String>();
            List<String> pfList = new List<String>();
            String token = "";
            int tokenStart, tokenEnd;
            tokenStart = tokenEnd = 0;

            for (int i = 0; i < str.Length; i++)
            {
                Char c = str[i];
                Console.WriteLine(i + "/" + tokenEnd + ": " + c);
                if (c == '(' || c == ')' || c == ' ' || c == '\t' || i == str.Length - 1)
                {
                    if (i == str.Length - 1)
                        tokenEnd++;

                    if (tokenEnd != 0)
                    {
                        token = str.Substring(tokenStart, tokenEnd);
                        Console.WriteLine("TOKEN (" + tokenStart + "=>" + (tokenStart + tokenEnd) + "): " + token);
                        state = ParserState.SEARCHING;
                        tokenEnd = 0;
                        tokenStart = i + 1;

                        if (token == "OR" || token == "AND")
                        {
                            if (opStack.Count == 0 || opStack.Peek() == "(")
                                opStack.Push(token);
                            else
                            {
                                String aToken;
                                while (opStack.Count > 0 && (aToken = opStack.Pop()) != "(")
                                    pfList.Add(aToken);

                                opStack.Push(token);
                            }
                        }
                        else
                            opStack.Push(token);
                    }
                    else
                        tokenStart++;

                    switch (c)
                    {
                        case '(':
                            opStack.Push("(");
                            break;
                        case ')':
                            String aToken;
                            while (opStack.Count > 0 && (aToken = opStack.Pop()) != "(")
                                pfList.Add(aToken);
                            break;
                    }
                }
                else
                {
                    switch (state)
                    {
                        case ParserState.SEARCHING:
                            tokenStart = i;
                            tokenEnd++;
                            state = ParserState.FOUND;
                            break;
                        case ParserState.FOUND:
                            tokenEnd++;
                            break;
                    }
                }
            }

            while (opStack.Count > 0)
                pfList.Add(opStack.Pop());

            foreach (String item in pfList)
                Console.Write(item + " ");
            Console.WriteLine("");

            foreach (String item in pfList)
            {
                if (item != "OR" && item != "AND")
                    pfStack.Push(item);
                else if (item == "OR" || item == "AND")
                {
                    String operand1 = pfStack.Pop();
                    String operand2 = pfStack.Pop();

                    Console.WriteLine("EVALUATING: " + operand1 + " " + item + " " + operand2);

                    pfStack.Push("[" + operand1 + " " + item + " " + operand2 + "]");
                }
            }

            Console.WriteLine("RESULT: " + pfStack.Pop());
        }
    }

    public class Key
    {
        public String keyName = "";
        public Boolean isActive = true;

        public Key()
        {
        }

        public Key(String keyName)
        {
            this.keyName = keyName;
        }
    }

    public class KeyStore
    {
        List<Key> keys = new List<Key>();

        public KeyStore()
        {
        }

        public void Clear()
        {
            keys.Clear();
        }

        public List<Key> getKeys()
        {
            return keys;
        }

        public void Activate(String keyName)
        {
            foreach (Key key in keys)
            {
                if (key.keyName == keyName)
                {
                    key.isActive = true;
                    return;
                }
            }

            Key aKey = new Key(keyName);
            aKey.isActive = true;

            keys.Add(aKey);
        }

        public void Deactivate(String keyName)
        {
            foreach (Key key in keys)
            {
                if (key.keyName == keyName)
                {
                    key.isActive = false;
                    return;
                }
            }

            Key aKey = new Key(keyName);
            aKey.isActive = false;

            keys.Add(aKey);
        }

        public Boolean Contains(String keyName)
        {
            foreach (Key key in keys)
            {
                if (key.keyName == keyName && key.isActive)
                    return true;
            }

            return false;
        }

        public Boolean Contains(Key key)
        {
            return Contains(key.keyName);
        }
    }

    public class Condition
    {
        public Key key;
        public Boolean negative = false;

        public Condition() { }
        public Condition(String keyName)
        {
            this.negative = false;
            if (keyName[0] == '!')
            {
                this.negative = true;
                keyName = keyName.Substring(1);
            }

            key = new Key(keyName);
        }

        public virtual Boolean Evaluate(KeyStore keys)
        {
            if ((keys.Contains(key) && !negative) || (!keys.Contains(key) && negative))
                return true;
            else
                return false;
        }
    }

    public enum BooleanOperator { OR, AND }

    public class ComplexCondition : Condition
    {
        public Condition operand1;
        public Condition operand2;
        public BooleanOperator op;

        public ComplexCondition(Condition operand1, Condition operand2, BooleanOperator op)
        {
            this.operand1 = operand1;
            this.operand2 = operand2;
            this.op = op;
        }

        public ComplexCondition(String operand1, String operand2, BooleanOperator op)
        {
            this.operand1 = new Condition(operand1);
            this.operand2 = new Condition(operand2);
        }

        public static Condition Parse(String str)
        {
            ParserState state = ParserState.SEARCHING;
            Stack<String> opStack = new Stack<String>();
            Stack<Condition> pfStack = new Stack<Condition>();
            List<String> pfList = new List<String>();
            String token = "";
            int tokenStart, tokenEnd;
            tokenStart = tokenEnd = 0;

            for (int i = 0; i < str.Length; i++)
            {
                Char c = str[i];
                if (c == '(' || c == ')' || c == ' ' || c == '\t' || i == str.Length - 1)
                {
                    if (i == str.Length - 1)
                        tokenEnd++;

                    if (tokenEnd != 0)
                    {
                        token = str.Substring(tokenStart, tokenEnd);
                        state = ParserState.SEARCHING;
                        tokenEnd = 0;
                        tokenStart = i + 1;

                        if (token == "OR" || token == "AND")
                        {
                            if (opStack.Count == 0 || opStack.Peek() == "(")
                                opStack.Push(token);
                            else
                            {
                                String aToken;
                                while (opStack.Count > 0 && (aToken = opStack.Pop()) != "(")
                                    pfList.Add(aToken);

                                opStack.Push(token);
                            }
                        }
                        else
                            opStack.Push(token);
                    }
                    else
                        tokenStart++;

                    switch (c)
                    {
                        case '(':
                            opStack.Push("(");
                            break;
                        case ')':
                            String aToken;
                            while (opStack.Count > 0 && (aToken = opStack.Pop()) != "(")
                                pfList.Add(aToken);
                            break;
                    }
                }
                else
                {
                    switch (state)
                    {
                        case ParserState.SEARCHING:
                            tokenStart = i;
                            tokenEnd++;
                            state = ParserState.FOUND;
                            break;
                        case ParserState.FOUND:
                            tokenEnd++;
                            break;
                    }
                }
            }

            while (opStack.Count > 0)
                pfList.Add(opStack.Pop());

            foreach (String item in pfList)
            {
                if (item != "OR" && item != "AND")
                    pfStack.Push(new Condition(item));
                else if (item == "OR" || item == "AND")
                {
                    if (pfStack.Count == 0)
                    {
                        Console.WriteLine("UNABLE TO PARSE EXPRESSION!");
                        return null;
                    }
                    Condition operand1 = pfStack.Pop();

                    if (pfStack.Count == 0)
                    {
                        Console.WriteLine("UNABLE TO PARSE EXPRESSION!");
                        return null;
                    }
                    Condition operand2 = pfStack.Pop();

                    switch (item)
                    {
                        case "OR":
                            pfStack.Push(new ComplexCondition(operand1, operand2, BooleanOperator.OR));
                            break;
                        case "AND":
                            pfStack.Push(new ComplexCondition(operand1, operand2, BooleanOperator.AND));
                            break;
                    }
                }
            }

            return pfStack.Pop();
        }

        public override Boolean Evaluate(KeyStore keys)
        {
            switch (op)
            {
                case BooleanOperator.AND:
                    return operand1.Evaluate(keys) && operand2.Evaluate(keys);
                case BooleanOperator.OR:
                    return operand1.Evaluate(keys) || operand2.Evaluate(keys);
            }

            return false;
        }
    }

    public class Actor
    {
        public String name;
    }

    public enum EventState {START, DIALOG, CHOICES, END}

    public class Event
    {
        public String text;
        public Location location;
        public int currentDialog = 0;
        public int currentElement = 0;
        public EventState state = EventState.START;

        public List<Dialog> dialogs = new List<Dialog>();
        public List<Dialog> validDialogs = new List<Dialog>();
        public List<Command> commands = new List<Command>();

        public List<ScriptElement> orderedElements = new List<ScriptElement>();
        public List<ScriptElement> validElements = new List<ScriptElement>();

        public List<Choice> choices = new List<Choice>();
        public List<Choice> validChoices = new List<Choice>();

        public EventState GetState()
        {
            return state;
        }

        public String Start()
        {
            currentDialog = 0;
            currentElement = 0;

            validChoices.Clear();
            validDialogs.Clear();
            validElements.Clear();

            if (orderedElements.Count == 0)
                state = EventState.END;

            state = EventState.DIALOG;

            return text;
        }

        public void Validate(KeyStore keys)
        {
            foreach (Dialog dialog in dialogs)
            {
                if (dialog.condition == null || dialog.condition.Evaluate(keys))
                    validDialogs.Add(dialog);
            }

            foreach (Choice choice in choices)
            {
                if (choice.condition == null || choice.condition.Evaluate(keys))
                    validChoices.Add(choice);
            }

            foreach (ScriptElement element in orderedElements)
            {
                if (element.condition == null || element.condition.Evaluate(keys))
                    validElements.Add(element);
            }
        }

        public ScriptElement NextElement()
        {
            if (currentElement < validElements.Count)
            {
                return validElements[currentElement++];
            }
            else
            {
                state = EventState.CHOICES;
                return null;
            }
        }

        public Dialog NextDialog()
        {
            if (currentDialog < validDialogs.Count)
            {
                return validDialogs[currentDialog++];
            }
            else
            {
                state = EventState.CHOICES;
                return null;
            }
        }

        public List<Choice> GetChoices()
        {
            state = EventState.END;
            return validChoices;
        }
    }

    public class Choice
    {
        public String text;
        public Event nextEvent;
        public List<Command> commands = new List<Command>();
        public Condition condition;
    }
}
