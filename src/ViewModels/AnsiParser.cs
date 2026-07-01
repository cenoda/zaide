using System;
using System.Collections.Generic;
using System.Text;

namespace Zaide.ViewModels;

/// <summary>
/// Pure ANSI/CSI parser for decoded terminal text. Maintains parser state
/// across calls so escape sequences can span chunk boundaries.
/// </summary>
internal sealed class AnsiParser
{
    private const int MaxUnsupportedStringLength = 4096;

    private readonly StringBuilder _printBuffer = new();
    private readonly StringBuilder _csiBuffer = new();

    private ParserState _state = ParserState.Ground;
    private UnsupportedState _unsupportedState = UnsupportedState.None;
    private bool _unsupportedEscSeen;
    private int _unsupportedLength;

    public IReadOnlyList<AnsiAction> Parse(ReadOnlySpan<char> chunk)
    {
        var actions = new List<AnsiAction>();

        foreach (char ch in chunk)
        {
            switch (_state)
            {
                case ParserState.Ground:
                    ProcessGround(ch, actions);
                    break;
                case ParserState.Escape:
                    ProcessEscape(ch, actions);
                    break;
                case ParserState.Csi:
                    ProcessCsi(ch, actions);
                    break;
                case ParserState.EscapeCharsetDesignator:
                    ProcessEscapeCharsetDesignator();
                    break;
                case ParserState.UnsupportedString:
                    ProcessUnsupportedString(ch, actions);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown parser state: {_state}");
            }
        }

        FlushPrint(actions);
        return actions;
    }

    private void ProcessGround(char ch, List<AnsiAction> actions)
    {
        if (ch == '\x1B')
        {
            FlushPrint(actions);
            _state = ParserState.Escape;
            return;
        }

        if (TryMapControl(ch, out var control))
        {
            FlushPrint(actions);
            actions.Add(new ExecuteAction(control));
            return;
        }

        if (!char.IsControl(ch))
        {
            _printBuffer.Append(ch);
        }
    }

    private void ProcessEscape(char ch, List<AnsiAction> actions)
    {
        switch (ch)
        {
            case '[':
                _csiBuffer.Clear();
                _state = ParserState.Csi;
                break;
            case ']':
                StartUnsupportedString(UnsupportedState.Osc);
                break;
            case 'P':
                StartUnsupportedString(UnsupportedState.Dcs);
                break;
            case '(':
            case ')':
            case '*':
            case '+':
                _state = ParserState.EscapeCharsetDesignator;
                break;
            case '7':
            case '8':
            case '=':
            case '>':
            case 'c':
                _state = ParserState.Ground;
                break;
            default:
                _state = ParserState.Ground;
                ProcessGround(ch, actions);
                break;
        }
    }

    private void ProcessCsi(char ch, List<AnsiAction> actions)
    {
        if (ch == '\x1B')
        {
            _csiBuffer.Clear();
            _state = ParserState.Escape;
            return;
        }

        if (IsCsiFinalByte(ch))
        {
            EmitSupportedCsi(ch, actions);
            _csiBuffer.Clear();
            _state = ParserState.Ground;
            return;
        }

        _csiBuffer.Append(ch);
    }

    private void ProcessEscapeCharsetDesignator()
    {
        _state = ParserState.Ground;
    }

    private void ProcessUnsupportedString(char ch, List<AnsiAction> actions)
    {
        if (_unsupportedState == UnsupportedState.Osc && ch == '\a')
        {
            ResetUnsupportedString();
            return;
        }

        if (_unsupportedEscSeen)
        {
            _unsupportedEscSeen = false;

            if (ch == '\\')
            {
                ResetUnsupportedString();
                return;
            }
        }

        _unsupportedEscSeen = ch == '\x1B';
        _unsupportedLength++;

        if (_unsupportedLength < MaxUnsupportedStringLength)
        {
            return;
        }

        ResetUnsupportedString();

        if (ch == '\x1B')
        {
            _state = ParserState.Escape;
        }
        else
        {
            ProcessGround(ch, actions);
        }
    }

    private void EmitSupportedCsi(char finalByte, List<AnsiAction> actions)
    {
        if (!IsSupportedCsiFinalByte(finalByte))
        {
            return;
        }

        string rawParameters = _csiBuffer.ToString();
        if (!IsSupportedCsiParameterBytes(rawParameters, finalByte))
        {
            return;
        }

        if (!TryParseParameters(rawParameters, finalByte, out var parameters))
        {
            return;
        }

        if (finalByte == 'h' || finalByte == 'l')
        {
            if (parameters.Length > 0 && parameters[0] == 2004)
            {
                actions.Add(new DecSetResetAction(parameters[0], finalByte == 'h'));
            }
        }
        else
        {
            actions.Add(new CsiDispatchAction(parameters, finalByte));
        }
    }

    private static bool HasSupportedCsiParameterBytes(string rawParameters)
    {
        foreach (char ch in rawParameters)
        {
            if ((ch >= '0' && ch <= '9') || ch == ';' || ch == '?')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool TryParseParameters(string rawParameters, char finalByte, out int[] parameters)
    {
        if (rawParameters.Length == 0)
        {
            parameters = GetDefaultParameters(finalByte);
            return true;
        }

        var parts = rawParameters.Split(';', ':');
        var values = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
            {
                values[i] = GetDefaultParameterValue(finalByte, i);
                continue;
            }

            var part = parts[i];
            if (part.StartsWith('?'))
            {
                part = part.Substring(1);
            }

            if (!int.TryParse(part, out values[i]))
            {
                parameters = Array.Empty<int>();
                return false;
            }
        }

        parameters = values;
        return true;
    }

    private static int[] GetDefaultParameters(char finalByte)
    {
        return finalByte switch
        {
            'A' or 'B' or 'C' or 'D' => [1],
            'H' => [1, 1],
            'J' or 'K' or 'm' => [0],
            _ => Array.Empty<int>()
        };
    }

    private static int GetDefaultParameterValue(char finalByte, int index)
    {
        return finalByte switch
        {
            'A' or 'B' or 'C' or 'D' => 1,
            'H' when index == 0 || index == 1 => 1,
            'J' or 'K' or 'm' => 0,
            _ => 0
        };
    }

    private static bool TryMapControl(char ch, out AnsiC0Control control)
    {
        switch (ch)
        {
            case '\r':
                control = AnsiC0Control.CarriageReturn;
                return true;
            case '\n':
                control = AnsiC0Control.LineFeed;
                return true;
            case '\b':
                control = AnsiC0Control.Backspace;
                return true;
            case '\t':
                control = AnsiC0Control.Tab;
                return true;
            case '\a':
                control = AnsiC0Control.Bell;
                return true;
            default:
                control = default;
                return false;
        }
    }

    private static bool IsCsiFinalByte(char ch) => ch >= 0x40 && ch <= 0x7E;

    private static bool IsSupportedCsiFinalByte(char ch)
    {
        return ch is 'A' or 'B' or 'C' or 'D' or 'H' or 'J' or 'K' or 'm' or 'h' or 'l';
    }

    private static bool IsSupportedCsiParameterBytes(string rawParameters, char finalByte)
    {
        if (finalByte != 'm')
        {
            return HasSupportedCsiParameterBytes(rawParameters);
        }

        foreach (char ch in rawParameters)
        {
            if ((ch >= '0' && ch <= '9') || ch == ';' || ch == ':')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void FlushPrint(List<AnsiAction> actions)
    {
        if (_printBuffer.Length == 0)
        {
            return;
        }

        actions.Add(new PrintAction(_printBuffer.ToString()));
        _printBuffer.Clear();
    }

    private void StartUnsupportedString(UnsupportedState unsupportedState)
    {
        _unsupportedState = unsupportedState;
        _unsupportedEscSeen = false;
        _unsupportedLength = 0;
        _state = ParserState.UnsupportedString;
    }

    private void ResetUnsupportedString()
    {
        _unsupportedState = UnsupportedState.None;
        _unsupportedEscSeen = false;
        _unsupportedLength = 0;
        _state = ParserState.Ground;
    }

    private enum ParserState
    {
        Ground,
        Escape,
        Csi,
        EscapeCharsetDesignator,
        UnsupportedString
    }

    private enum UnsupportedState
    {
        None,
        Osc,
        Dcs
    }
}

internal abstract record AnsiAction;

internal sealed record PrintAction(string Text) : AnsiAction;

internal sealed record ExecuteAction(AnsiC0Control Control) : AnsiAction;

internal sealed record CsiDispatchAction(int[] Parameters, char FinalByte) : AnsiAction;

internal sealed record DecSetResetAction(int Mode, bool Enabled) : AnsiAction;

internal enum AnsiC0Control
{
    Bell,
    Backspace,
    Tab,
    LineFeed,
    CarriageReturn
}
