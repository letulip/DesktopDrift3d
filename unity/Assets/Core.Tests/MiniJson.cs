// Test-only helper: a minimal JSON reader. It exists because System.Text.Json is unavailable
// inside Unity's test assembly. Objects -> Dictionary<string, object?>, arrays -> List<object?>,
// numbers -> double, strings -> string, true/false -> bool, null -> null.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DesktopDrift.Core.Tests
{
    internal static class MiniJson
    {
        public static object? Parse(string json)
        {
            var r = new Reader(json);
            r.SkipWs();
            object? v = r.ReadValue();
            r.SkipWs();
            if (!r.AtEnd) throw r.Fail("unexpected trailing content");
            return v;
        }

        private sealed class Reader
        {
            private readonly string _s;
            private int _i;

            public Reader(string s) { _s = s; _i = 0; }

            public bool AtEnd { get { return _i >= _s.Length; } }

            public FormatException Fail(string msg)
            {
                return new FormatException("MiniJson: " + msg + " at position " + _i);
            }

            public void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _i++;
                    else break;
                }
            }

            public object? ReadValue()
            {
                if (AtEnd) throw Fail("unexpected end of input");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ExpectWord("true"); return true;
                    case 'f': ExpectWord("false"); return false;
                    case 'n': ExpectWord("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Fail("unexpected character '" + c + "'");
                }
            }

            private void ExpectWord(string w)
            {
                if (string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0) throw Fail("expected '" + w + "'");
                _i += w.Length;
            }

            private Dictionary<string, object?> ReadObject()
            {
                var d = new Dictionary<string, object?>();
                _i++; // {
                SkipWs();
                if (!AtEnd && _s[_i] == '}') { _i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (AtEnd || _s[_i] != '"') throw Fail("expected string key");
                    string key = ReadString();
                    SkipWs();
                    if (AtEnd || _s[_i] != ':') throw Fail("expected ':'");
                    _i++;
                    SkipWs();
                    d[key] = ReadValue();
                    SkipWs();
                    if (AtEnd) throw Fail("unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return d; }
                    throw Fail("expected ',' or '}'");
                }
            }

            private List<object?> ReadArray()
            {
                var l = new List<object?>();
                _i++; // [
                SkipWs();
                if (!AtEnd && _s[_i] == ']') { _i++; return l; }
                while (true)
                {
                    SkipWs();
                    l.Add(ReadValue());
                    SkipWs();
                    if (AtEnd) throw Fail("unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return l; }
                    throw Fail("expected ',' or ']'");
                }
            }

            private double ReadNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
                if (_i < _s.Length && _s[_i] == '.')
                {
                    _i++;
                    while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
                }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    _i++;
                    if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9') _i++;
                }
                string token = _s.Substring(start, _i - start);
                double v;
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    _i = start;
                    throw Fail("bad number '" + token + "'");
                }
                return v;
            }

            private string ReadString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Fail("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Fail("unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Fail("truncated \\u escape");
                            int code;
                            if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
                                throw Fail("bad \\u escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default:
                            _i--;
                            throw Fail("bad escape '\\" + e + "'");
                    }
                }
            }
        }
    }
}
