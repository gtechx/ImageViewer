﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TextureViewer.Models.Filter
{
    public class FilterLoader
    {
        public string ShaderSource { get; private set; } = "";
        public List<IFilterParameter> Parameters { get; } = new List<IFilterParameter>();
        public bool IsSepa { get; private set; } = false;
        public bool IsSingleInvocation { get; private set; } = true;
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Filename { get; }
        public List<FilterTextureParameterModel> TextureParameters { get; } = new List<FilterTextureParameterModel>();

        public FilterLoader(string filename)
        {
            this.Filename = filename;
            Name = filename;
            Description = "";

            int lineNumber = 1;

            // Read the file and display it line by line.
            System.IO.StreamReader file =
                new System.IO.StreamReader(filename);

            try
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if (line.StartsWith("#paramprop"))
                    {
                        HandleParamprop(GetParameters(line.Substring("#paramprop".Length)));
                        ShaderSource += "\n"; // remember line for error information
                    }
                    else if (line.StartsWith("#param"))
                    {
                        HandleParam(GetParameters(line.Substring("#param".Length)));
                        ShaderSource += "\n"; // remember line for error information
                    }
                    else if(line.StartsWith("#texture"))
                    {
                        HandleTexture(GetParameters(line.Substring("#texture".Length)));
                        ShaderSource += "\n"; // remember line for error information
                    }
                    else if (line.StartsWith("#setting"))
                    {
                        var whole = line.Substring("#setting".Length);
                        var parameters = GetParameters(whole);
                        // get the second parameter as one string (without , seperation)
                        try
                        {
                            var idx = whole.IndexOf(",", StringComparison.Ordinal);
                            whole = whole.Substring(idx + 1);
                        }
                        catch (Exception)
                        {
                            // no second parameter available
                            whole = "";
                        }

                        whole = whole.TrimStart(' ');
                        whole = whole.TrimEnd(' ');

                        HandleSetting(parameters, whole);
                        ShaderSource += "\n"; // remember line for error information
                    }
                    else if (line.StartsWith("#keybinding"))
                    {
                        HandleKeybinding(GetParameters(line.Substring("#keybinding".Length)));
                        ShaderSource += "\n"; // remember line for error information
                    }
                    else
                    {
                        ShaderSource += line + "\n";
                    }
                    ++lineNumber;
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message + " at line " + lineNumber);
            }
            finally
            {
                file.Close();
            }
        }

        private void HandleTexture(string[] pars)
        {
            if (pars.Length < 2)
                throw new Exception("not enough arguments for #texture provided");
            var name = pars[0];
            if (!Int32.TryParse(pars[1], out int binding))
                throw new Exception("binding must be a number");
            if (binding == 0)
                throw new Exception("binding 0 is reserved");
            // TODO throw if max texture units exceeded
            // make sure that binding was not used
            if (TextureParameters.Find((item) => item.Binding == binding) != null)
                throw new Exception($"texture binding {binding} was used more than once");

            TextureParameters.Add(new FilterTextureParameterModel(name, binding));
        }

        private void HandleParamprop(string[] pars)
        {
            if (pars.Length < 2)
                throw new Exception("not enough arguments for #paramprops provided");
            var matchingParam = FindMatchingParameter(pars[0]);

            if (!Enum.TryParse(pars[1], true, out ActionType atype))
                throw new Exception("unknown paramprops action " + pars[1]);
            switch (atype)
            {
                case ActionType.OnAdd:
                case ActionType.OnSubtract:
                    {
                        if (pars.Length < 4)
                            throw new Exception("not enough arguments for #paramprops provided");

                        var modType = GetModificationType(pars[3]);

                        switch (matchingParam.GetParamterType())
                        {
                            case ParameterType.Float:
                                AddParameterAction(matchingParam.GetFloatModel(), pars[2], modType, atype);
                                break;
                            case ParameterType.Int:
                                AddParameterAction(matchingParam.GetIntModel(), pars[2], modType, atype);
                                break;
                            case ParameterType.Bool:
                                AddParameterAction(matchingParam.GetBoolModel(), pars[2], modType, atype);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    break;
                default:
                    throw new Exception("unknown paramprops action " + pars[1]);
            }
        }

        private void AddParameterAction(IntFilterParameterModel model, string value, ModificationType modType, ActionType actionType)
        {
            var val = GetIntValue(value);
            var action = new IntFilterParameterModel.IntParameterAction(val, modType);
            model.Actions[actionType] = action;
        }

        private void AddParameterAction(FloatFilterParameterModel model, string value, ModificationType modType, ActionType actionType)
        {
            var val = GetFloatValue(value);
            var action = new FloatFilterParameterModel.FloatParameterAction(val, modType);
            model.Actions[actionType] = action;
        }

        private void AddParameterAction(BoolFilterParameterModel model, string value, ModificationType modType, ActionType actionType)
        {
            var val = GetBoolValue(value);
            var action = new BoolFilterParameterModel.BoolParameterAction(val, modType);
            model.Actions[actionType] = action;
        }

        private ModificationType GetModificationType(string type)
        {
            switch (type)
            {
                case "add":
                    return ModificationType.Add;
                case "multiply":
                    return ModificationType.Multiply;
                case "set":
                    return ModificationType.Set;
                default:
                    throw new Exception("invalid keybinding operation");
            }
        }

        private void HandleKeybinding(string[] pars)
        {
            if (pars.Length < 4)
                throw new Exception("not enough arguments for #keybinding provided");

            var matchingParam = FindMatchingParameter(pars[0]);

            var modType = GetModificationType(pars[3]);

            // try to parse key
            if (!Enum.TryParse<System.Windows.Input.Key>(pars[1], out var key))
                throw new Exception("could not match key in keybinding");

            switch (matchingParam.GetParamterType())
            {
                case ParameterType.Float:
                    AddKeybinding(matchingParam.GetFloatModel(), pars[2], modType, key);
                    break;
                case ParameterType.Int:
                    AddKeybinding(matchingParam.GetIntModel(), pars[2], modType, key);
                    break;
                case ParameterType.Bool:
                    AddKeybinding(matchingParam.GetBoolModel(), pars[2], modType, key);
                    break;
            }
        }

        private void AddKeybinding(IntFilterParameterModel model, string value, ModificationType modType, Key key)
        {
            var val = GetIntValue(value);
            var binding = new IntFilterParameterModel.IntParameterAction(val, modType);
            model.Keybindings[key] = binding;
        }

        private void AddKeybinding(FloatFilterParameterModel model, string value, ModificationType modType, Key key)
        {
            var val = GetFloatValue(value);
            var binding = new FloatFilterParameterModel.FloatParameterAction(val, modType);
            model.Keybindings[key] = binding;
        }

        private void AddKeybinding(BoolFilterParameterModel model, string value, ModificationType modType, Key key)
        {
            var val = GetBoolValue(value);
            var binding = new BoolFilterParameterModel.BoolParameterAction(val, modType);
            model.Keybindings[key] = binding;
        }

        private void HandleParam(string[] pars)
        {
            if (pars.Length < 3)
                throw new Exception("not enough arguments for #param provided");

            if (!Int32.TryParse(pars[1], out var location))
                throw new Exception("location must be a number");

            ParameterType type;
            if (pars[2].ToLower().Equals("float"))
                type = ParameterType.Float;
            else if (pars[2].ToLower().Equals("int"))
                type = ParameterType.Int;
            else if (pars[2].ToLower().Equals("bool"))
                type = ParameterType.Bool;
            else throw new Exception("unknown parameter type " + pars[2]);

            switch (type)
            {
                case ParameterType.Float:
                    AddFloatParam(location, pars);
                    break;
                case ParameterType.Int:
                    AddIntParam(location, pars);
                    break;
                case ParameterType.Bool:
                    AddBoolParam(location, pars);
                    break;
            }
        }

        private void AddIntParam(int location, string[] pars)
        {
            var def = pars.Length >= 4 ? GetIntValue(pars[3]) : 0;

            var min = pars.Length >= 5 ? GetIntValue(pars[4]) : Int32.MinValue;

            var max = pars.Length >= 6 ? GetIntValue(pars[5]) : Int32.MaxValue;

            Parameters.Add(new IntFilterParameterModel(pars[0], location, min, max, def));
        }

        private void AddFloatParam(int location, string[] pars)
        {
            var def = pars.Length >= 4 ? GetFloatValue(pars[3]) : 0.0f;

            var min = pars.Length >= 5 ? GetFloatValue(pars[4]) : Single.MinValue;

            var max = pars.Length >= 6 ? GetFloatValue(pars[5]) : Single.MaxValue;

            Parameters.Add(new FloatFilterParameterModel(pars[0], location, min, max, def));
        }

        private void AddBoolParam(int location, string[] pars)
        {
            var def = pars.Length >= 4 && GetBoolValue(pars[3]);

            Parameters.Add(new BoolFilterParameterModel(pars[0], location, false, true, def));
        }

        private bool GetBoolValue(string argument)
        {
            switch (argument.ToLower())
            {
                case "true":
                    return true;
                case "false":
                    return false;
            }
            throw new Exception("cannot convert argument to bool. expected either true or false");
        }

        private int GetIntValue(string argument)
        {
            if(!Int32.TryParse(argument, NumberStyles.Integer, App.GetCulture(), out var res))
                throw new Exception("cannot convert argument to int");
            return res;
        }

        private float GetFloatValue(string argument)
        {
            if (!Single.TryParse(argument, NumberStyles.Float, App.GetCulture(), out var res))
                throw new Exception("cannot convert argument to int");
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p">komma seperated parameters</param>
        /// <param name="wholeString">the second parameter wihtout seperation of kommas (for description)</param>
        private void HandleSetting(string[] p, string wholeString)
        {
            if (p.Length < 2)
                throw new Exception("not enough arguments for #setting provided");
            switch (p[0].ToLower())
            {
                case "sepa":
                    IsSepa = p[1].ToLower().Equals("true");
                    break;
                case "title":
                    Name = wholeString;
                    break;
                case "description":
                    Description = wholeString;
                    break;
                case "singleinvocation":
                    IsSingleInvocation = p[1].ToLower().Equals("true");
                    break;
                default:
                    throw new Exception("unknown setting " + p[0]);
            }
        }

        private static string[] GetParameters(string s)
        {
            string[] pars = s.Split(',');
            // remove some white spaces
            for (int i = 0; i < pars.Length; ++i)
            {
                pars[i] = pars[i].TrimStart(' ');
                pars[i] = pars[i].TrimEnd(' ');
            }

            return pars;
        }

        private IFilterParameter FindMatchingParameter(string name)
        {
            foreach (var parameter in Parameters)
            {
                if (parameter.GetBase().Name == name)
                {
                    return parameter;
                }
            }
            throw new Exception("could not match keybinding with name: " + name + " to any parameter");
        }
    }
}
