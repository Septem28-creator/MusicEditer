using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piano.Parser
{
    /// <summary>
    /// 词法分析器类
    /// </summary>
    public class Lexer
    {
        private readonly string _source;
        private int _position;
        private readonly List<Token> _tokens;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="source">源代码字符串</param>
        public Lexer(string source)
        {
            _source = source;
            _position = 0;
            _tokens = new List<Token>();
        }

        /// <summary>
        /// 执行词法分析
        /// </summary>
        /// <returns>词法单元列表</returns>
        public List<Token> Tokenize()
        {
            while (!IsAtEnd())
            {
                char c = Advance();
                
                switch (c)
                {
                    // 跳过空白字符
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        break;
                    
                    // 单字符符号
                    case '(':
                        AddToken(TokenType.LEFT_PAREN, "(");
                        break;
                    case ')':
                        AddToken(TokenType.RIGHT_PAREN, ")");
                        break;
                    case '{':
                        AddToken(TokenType.LEFT_BRACE, "{");
                        break;
                    case '}':
                        AddToken(TokenType.RIGHT_BRACE, "}");
                        break;
                    case ',':
                        AddToken(TokenType.COMMA, ",");
                        break;
                    case ':':
                        AddToken(TokenType.COLON, ":");
                        break;
                    case '/':
                        if (Match('/'))
                        {
                            // 单行注释
                            while (!IsAtEnd() && Peek() != '\n')
                                Advance();
                            AddToken(TokenType.COMMENT, "//");
                        }
                        else if (Match('*'))
                        {
                            // 多行注释
                            while (!IsAtEnd() && !(Peek() == '*' && PeekNext() == '/'))
                                Advance();
                            if (!IsAtEnd())
                            {
                                Advance(); // 跳过 '*'
                                Advance(); // 跳过 '/'
                            }
                            AddToken(TokenType.COMMENT, "/* */");
                        }
                        else
                        {
                            AddToken(TokenType.SLASH, "/");
                        }
                        break;
                    case '.':
                        AddToken(TokenType.DOT, ".");
                        break;
                    case '|':
                        AddToken(TokenType.BAR, "|");
                        break;
                    case '~':
                        AddToken(TokenType.TILDE, "~");
                        break;
                    case '-':
                        AddToken(TokenType.TIE, "-");
                        break;
                    case '\\':
                        // 检查是否是双反斜杠（滑音）
                        if (Match('\\'))
                        {
                            AddToken(TokenType.GLISSANDO, "\\");
                        }
                        else
                        {
                            AddToken(TokenType.BACKSLASH, "\\");
                        }
                        break;
                    case '[':
                        ScanKeyword();
                        break;
                    case ']':
                        // 右方括号不单独处理，在关键字扫描中处理
                        break;
                    
                    // 音名
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                    case 'F':
                    case 'G':
                        ScanNoteName(c);
                        break;
                    
                    // 升降号
                    case '#':
                        AddToken(TokenType.SHARP, "#");
                        break;
                    case 'b':
                        AddToken(TokenType.FLAT, "b");
                        break;
                    
                    // 数字
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        ScanNumber(c);
                        break;
                    
                    // 休止符
                    case 'R':
                        if (Peek() == '(')
                        {
                            AddToken(TokenType.REST, "R");
                        }
                        else
                        {
                            ScanIdentifier(c);
                        }
                        break;
                    
                    // 其他字符作为标识符处理
                    default:
                        ScanIdentifier(c);
                        break;
                }
            }
            
            _tokens.Add(new Token(TokenType.EOF, "", _position));
            return _tokens;
        }

        /// <summary>
        /// 扫描音名
        /// </summary>
        /// <param name="first">第一个字符</param>
        private void ScanNoteName(char first)
        {
            AddToken(TokenType.NOTE_NAME, first.ToString());
        }

        /// <summary>
        /// 扫描数字（用于时值）
        /// </summary>
        /// <param name="first">第一个数字字符</param>
        private void ScanNumber(char first)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(first);
            
            while (!IsAtEnd() && (IsDigit(Peek()) || Peek() == '/' || Peek() == '.'))
            {
                sb.Append(Advance());
            }
            
            AddToken(TokenType.NUMBER, sb.ToString());
        }

        /// <summary>
        /// 扫描标识符
        /// </summary>
        /// <param name="first">第一个字符</param>
        private void ScanIdentifier(char first)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(first);
            
            while (!IsAtEnd() && IsAlphaNumeric(Peek()))
            {
                sb.Append(Advance());
            }
            
            string text = sb.ToString();
            
            // 简单处理关键字
            // 检查是否为声部标记
            if (text.StartsWith("V") && text.Length == 2 && char.IsDigit(text[1]))
            {
                AddToken(TokenType.VOICE, text);
            }
            // 检查是否为其他关键字
            else if (text.Length == 1 && text == "V")
            {
                AddToken(TokenType.VOICE, text);
            }
            else
            {
                switch (text)
                {
                    case "VOICE":
                        AddToken(TokenType.VOICE_BLOCK, text);
                        break;
                    case "/VOICE":
                        AddToken(TokenType.END_VOICE, text);
                        break;
                    case "PART":
                        AddToken(TokenType.PART, text);
                        break;
                    case "/PART":
                        AddToken(TokenType.END_PART, text);
                        break;
                    case "BPM":
                        AddToken(TokenType.BPM, text);
                        break;
                    case "KEY":
                        AddToken(TokenType.KEY, text);
                        break;
                    case "INTRO":
                        AddToken(TokenType.INTRO, text);
                        break;
                    case "INTERLUDE":
                        AddToken(TokenType.INTERLUDE, text);
                        break;
                    case "REPEAT":
                        AddToken(TokenType.REPEAT, text);
                        break;
                    case "p":
                    case "pp":
                    case "mp":
                    case "mf":
                    case "f":
                    case "ff":
                    case "fff":
                        AddToken(TokenType.DYNAMICS, text);
                        break;
                    default:
                        AddToken(TokenType.IDENTIFIER, text);
                        break;
                }
            }
        }

        /// <summary>
        /// 扫描关键字（如[BPM=120]）
        /// </summary>
        private void ScanKeyword()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            
            while (!IsAtEnd() && Peek() != ']')
            {
                sb.Append(Advance());
            }
            
            if (!IsAtEnd())
            {
                sb.Append(Advance()); // 跳过 ']'
            }
            
            string text = sb.ToString();
            
            // 简单处理关键字
            if (text.StartsWith("[BPM="))
            {
                AddToken(TokenType.BPM, text);
            }
            else if (text.StartsWith("[KEY="))
            {
                AddToken(TokenType.KEY, text);
            }
            else if (text == "[INTRO]")
            {
                AddToken(TokenType.INTRO, text);
            }
            else if (text == "[INTERLUDE]")
            {
                AddToken(TokenType.INTERLUDE, text);
            }
            else if (text == "[REPEAT]")
            {
                AddToken(TokenType.REPEAT, text);
            }
            else if (text == "[/REPEAT]")
            {
                AddToken(TokenType.END_REPEAT, text);
            }
            else if (text == "[VOICE]")
            {
                AddToken(TokenType.VOICE_BLOCK, text);
            }
            else if (text == "[/VOICE]")
            {
                AddToken(TokenType.END_VOICE, text);
            }
            else if (text == "[VOICES]")
            {
                AddToken(TokenType.VOICES, text);
            }
            else if (text == "[/VOICES]")
            {
                AddToken(TokenType.END_VOICES, text);
            }
            else if (text.StartsWith("[PART-") && text.EndsWith("]"))
            {
                AddToken(TokenType.PART, text);
            }
            else if (text.StartsWith("[/PART-") && text.EndsWith("]"))
            {
                AddToken(TokenType.END_PART, text);
            }
            else if (text == "[PART]")
            {
                AddToken(TokenType.PART, text);
            }
            else if (text == "[/PART]")
            {
                AddToken(TokenType.END_PART, text);
            }
            else
            {
                AddToken(TokenType.IDENTIFIER, text);
            }
        }

        /// <summary>
        /// 添加词法单元
        /// </summary>
        /// <param name="type">词法单元类型</param>
        /// <param name="lexeme">词素</param>
        private void AddToken(TokenType type, string lexeme)
        {
            _tokens.Add(new Token(type, lexeme, _position - lexeme.Length));
        }

        /// <summary>
        /// 判断是否到达源代码末尾
        /// </summary>
        /// <returns>是否到达末尾</returns>
        private bool IsAtEnd()
        {
            return _position >= _source.Length;
        }

        /// <summary>
        /// 获取当前字符并前进到下一个字符
        /// </summary>
        /// <returns>当前字符</returns>
        private char Advance()
        {
            return _source[_position++];
        }

        /// <summary>
        /// 查看当前字符但不前进
        /// </summary>
        /// <returns>当前字符</returns>
        private char Peek()
        {
            if (IsAtEnd()) return '\0';
            return _source[_position];
        }

        /// <summary>
        /// 查看下一个字符但不前进
        /// </summary>
        /// <returns>下一个字符</returns>
        private char PeekNext()
        {
            if (_position + 1 >= _source.Length) return '\0';
            return _source[_position + 1];
        }

        /// <summary>
        /// 判断下一个字符是否匹配指定字符
        /// </summary>
        /// <param name="expected">期望的字符</param>
        /// <returns>是否匹配</returns>
        private bool Match(char expected)
        {
            if (IsAtEnd()) return false;
            if (_source[_position] != expected) return false;
            
            _position++;
            return true;
        }

        /// <summary>
        /// 判断字符是否为数字
        /// </summary>
        /// <param name="c">字符</param>
        /// <returns>是否为数字</returns>
        private bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        /// <summary>
        /// 判断字符是否为字母或数字
        /// </summary>
        /// <param name="c">字符</param>
        /// <returns>是否为字母或数字</returns>
        private bool IsAlphaNumeric(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '=';
        }
    }
}