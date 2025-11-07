using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piano.Parser
{
    /// <summary>
    /// 语法分析器类
    /// </summary>
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _current;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="tokens">词法单元列表</param>
        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _current = 0;
        }

        /// <summary>
        /// 解析音乐作品
        /// </summary>
        /// <returns>抽象语法树根节点</returns>
        public ScoreNode Parse()
        {
            ScoreNode score = new ScoreNode();
            
            // 解析全局设置（BPM、调号等）
            while (!IsAtEnd() && Peek().Type != TokenType.EOF)
            {
                if (Match(TokenType.BPM))
                {
                    score.Tempo = ParseTempo();
                }
                else if (Match(TokenType.KEY))
                {
                    score.Key = ParseKey();
                }
                else if (Match(TokenType.VOICE))
                {
                    // 解析声部
                    var voice = ParseVoice();
                    score.Voices.Add(voice);
                }
                else if (Match(TokenType.VOICE_BLOCK))
                {
                    // 解析基于标签的声部块 [VOICE]
                    var voices = ParseVoiceBlock();
                    score.Voices.AddRange(voices);
                }
                else if (Match(TokenType.VOICES))
                {
                    // 解析新的VOICES语法 [VOICES][PART-A]...[PART-A][PART-B]...[/PART-B]...[/VOICES]
                    var voices = ParseVoicesBlock();
                    score.Voices.AddRange(voices);
                }
                else if (Match(TokenType.INTRO) || Match(TokenType.INTERLUDE) || Match(TokenType.REPEAT))
                {
                    score.Sections.Add(ParseSection());
                }
                else
                {
                    // 如果没有明确的段落标记，创建一个默认段落
                    SectionNode defaultSection = new SectionNode { SectionType = "DEFAULT" };
                    while (!IsAtEnd() && Peek().Type != TokenType.EOF && 
                           !Check(TokenType.INTRO) && !Check(TokenType.INTERLUDE) && !Check(TokenType.REPEAT) &&
                           !Check(TokenType.VOICE) && !Check(TokenType.VOICE_BLOCK))
                    {
                        defaultSection.Measures.Add(ParseMeasure());
                    }
                    score.Sections.Add(defaultSection);
                }
            }
            
            return score;
        }

        /// <summary>
        /// 解析速度标记
        /// </summary>
        /// <returns>速度节点</returns>
        private TempoNode ParseTempo()
        {
            Token token = Previous();
            // 简化处理，从标记中提取BPM值
            string bpmText = token.Lexeme.Replace("[BPM=", "").Replace("]", "");
            if (int.TryParse(bpmText, out int bpm))
            {
                return new TempoNode { BPM = bpm, Position = token.Position };
            }
            else
            {
                throw new ParseException($"Invalid BPM value: {bpmText}", token.Position);
            }
        }

        /// <summary>
        /// 解析调号
        /// </summary>
        /// <returns>调号节点</returns>
        private KeyNode ParseKey()
        {
            Token token = Previous();
            // 简化处理，从标记中提取调号
            string keyText = token.Lexeme.Replace("[KEY=", "").Replace("]", "");
            return new KeyNode { Key = keyText, Position = token.Position };
        }

        /// <summary>
        /// 解析段落
        /// </summary>
        /// <returns>段落节点</returns>
        private SectionNode ParseSection()
        {
            Token token = Previous();
            SectionNode section = new SectionNode();
            
            // 确定段落类型
            switch (token.Type)
            {
                case TokenType.INTRO:
                    section.SectionType = "INTRO";
                    break;
                case TokenType.INTERLUDE:
                    section.SectionType = "INTERLUDE";
                    break;
                case TokenType.REPEAT:
                    section.SectionType = "REPEAT";
                    break;
            }
            
            section.Position = token.Position;
            
            // 解析段落中的小节，直到结束标记或新的段落标记
            while (!IsAtEnd() && Peek().Type != TokenType.EOF &&
                   !Check(TokenType.INTRO) && !Check(TokenType.INTERLUDE) && 
                   !Check(TokenType.REPEAT) && !Check(TokenType.END_REPEAT))
            {
                section.Measures.Add(ParseMeasure());
            }
            
            // 如果是重复段，跳过结束标记
            if (token.Type == TokenType.REPEAT && Check(TokenType.END_REPEAT))
            {
                Advance();
            }
            
            return section;
        }

        /// <summary>
        /// 解析小节
        /// </summary>
        /// <returns>小节节点</returns>
        private MeasureNode ParseMeasure()
        {
            MeasureNode measure = new MeasureNode();
            
            // 解析小节中的元素，直到小节线或文件结束
            while (!IsAtEnd() && Peek().Type != TokenType.EOF && Peek().Type != TokenType.BAR)
            {
                if (Check(TokenType.NOTE_NAME))
                {
                    measure.Elements.Add(ParseNote());
                }
                else if (Check(TokenType.LEFT_BRACE))
                {
                    measure.Elements.Add(ParseChord());
                }
                else if (Check(TokenType.REST))
                {
                    measure.Elements.Add(ParseRest());
                }
                else if (Check(TokenType.DYNAMICS))
                {
                    // 强弱记号通常与音符关联，这里简化处理
                    Advance();
                }
                else if (Check(TokenType.VOICE))
                {
                    // 声部标记，跳出当前小节解析
                    break;
                }
                else
                {
                    // 跳过其他不识别的标记
                    Advance();
                }
            }
            
            // 跳过小节线
            if (Check(TokenType.BAR))
            {
                Advance();
            }
            
            return measure;
        }

        /// <summary>
        /// 解析声部
        /// </summary>
        /// <returns>声部节点</returns>
        private VoiceNode ParseVoice()
        {
            Token token = Previous();
            
            // 从标记中提取声部编号（如 V0, V1, V2 等）
            if (token.Lexeme.Length <= 1 || token.Lexeme[0] != 'V' || !char.IsDigit(token.Lexeme[1]))
            {
                throw new ParseException("Invalid voice marker format. Expected V followed by a number.", token.Position);
            }
            
            string voiceNumberText = token.Lexeme.Substring(1);
            if (!int.TryParse(voiceNumberText, out int voiceNumber))
            {
                throw new ParseException($"Invalid voice number: {voiceNumberText}", token.Position);
            }

            // 不再期望冒号，因为声部标记格式是 V0 而不是 V:0

            VoiceNode voice = new VoiceNode
            {
                VoiceNumber = voiceNumber,
                Position = token.Position
            };

            // 解析声部中的小节，直到遇到其他声部或文件结束
            while (!IsAtEnd() && Peek().Type != TokenType.EOF &&
                   Peek().Type != TokenType.VOICE && 
                   !Check(TokenType.INTRO) && !Check(TokenType.INTERLUDE) && 
                   !Check(TokenType.REPEAT) && !Check(TokenType.END_REPEAT) &&
                   !Check(TokenType.VOICE_BLOCK))
            {
                voice.Measures.Add(ParseMeasure());
            }

            return voice;
        }
        
        /// <summary>
        /// 解析基于标签的声部块
        /// </summary>
        /// <returns>声部节点列表</returns>
        private List<VoiceNode> ParseVoiceBlock()
        {
            var voices = new List<VoiceNode>();
            int voiceIndex = 0;
            
            // 解析PART块，直到遇到结束标记
            while (!IsAtEnd() && Peek().Type != TokenType.EOF && 
                   !Check(TokenType.END_VOICE))
            {
                if (Match(TokenType.PART))
                {
                    // 获取PART标记的完整文本
                    string partText = Previous().Lexeme;
                    string partName = partText;
                    
                    // 如果是带名称的PART标记（如 [PART-SOPRANO]），提取名称部分
                    if (partText.StartsWith("[PART-") && partText.EndsWith("]"))
                    {
                        partName = partText.Substring(6, partText.Length - 7); // 提取名称部分
                    }
                    
                    // 创建新的声部节点
                    var voice = new VoiceNode
                    {
                        VoiceNumber = voiceIndex++,
                        Position = Previous().Position
                    };
                    
                    // 解析小节，直到遇到结束标记
                    while (!IsAtEnd() && Peek().Type != TokenType.EOF && 
                           !Check(TokenType.END_PART) && !Check(TokenType.PART) && 
                           !Check(TokenType.END_VOICE))
                    {
                        voice.Measures.Add(ParseMeasure());
                    }
                    
                    voices.Add(voice);
                    
                    // 期望结束标记
                    if (Match(TokenType.END_PART))
                    {
                        // 检查结束标记是否匹配
                        string endPartText = Previous().Lexeme;
                        // 对于带名称的PART标记，我们只需要检查是否是以[/PART-开头并以]结尾
                        if (!endPartText.StartsWith("[/PART-") || !endPartText.EndsWith("]"))
                        {
                            throw new ParseException($"Expected [/PART-NAME], but found {endPartText}", Previous().Position);
                        }
                    }
                    else
                    {
                        throw new ParseException("Expected [/PART-NAME] to close part block", Peek().Position);
                    }
                }
                else
                {
                    // 跳过其他标记
                    Advance();
                }
            }
            
            // 期望结束标记
            if (!Match(TokenType.END_VOICE))
            {
                throw new ParseException("Expected [/VOICE] to close voice block", Peek().Position);
            }
            
            return voices;
        }

        /// <summary>
        /// 解析VOICES块 [VOICES][PART-A]...[PART-A][PART-B]...[/PART-B]...[/VOICES]
        /// </summary>
        /// <returns>声部节点列表</returns>
        private List<VoiceNode> ParseVoicesBlock()
        {
            var voices = new List<VoiceNode>();
            int voiceIndex = 0;
            
            // 解析PART块，直到遇到结束标记
            while (!IsAtEnd() && Peek().Type != TokenType.EOF && 
                   !Check(TokenType.END_VOICES))
            {
                if (Match(TokenType.PART))
                {
                    // 获取PART标记的完整文本
                    string partText = Previous().Lexeme;
                    string partName = partText;
                    
                    // 如果是带名称的PART标记（如 [PART-SOPRANO]），提取名称部分
                    if (partText.StartsWith("[PART-") && partText.EndsWith("]"))
                    {
                        partName = partText.Substring(6, partText.Length - 7); // 提取名称部分
                    }
                    
                    // 创建新的声部节点
                    var voice = new VoiceNode
                    {
                        VoiceNumber = voiceIndex++,
                        Position = Previous().Position
                    };
                    
                    // 解析小节，直到遇到结束标记
                    while (!IsAtEnd() && Peek().Type != TokenType.EOF && 
                           !Check(TokenType.END_PART) && !Check(TokenType.PART) && 
                           !Check(TokenType.END_VOICES))
                    {
                        voice.Measures.Add(ParseMeasure());
                    }
                    
                    voices.Add(voice);
                    
                    // 期望结束标记
                    if (Match(TokenType.END_PART))
                    {
                        // 检查结束标记是否匹配
                        string endPartText = Previous().Lexeme;
                        // 对于带名称的PART标记，我们只需要检查是否是以[/PART-开头并以]结尾
                        if (!endPartText.StartsWith("[/PART-") || !endPartText.EndsWith("]"))
                        {
                            throw new ParseException($"Expected [/PART-NAME], but found {endPartText}", Previous().Position);
                        }
                    }
                    else
                    {
                        throw new ParseException("Expected [/PART-NAME] to close part block", Peek().Position);
                    }
                }
                else
                {
                    // 跳过其他标记
                    Advance();
                }
            }
            
            // 期望结束标记
            if (!Match(TokenType.END_VOICES))
            {
                throw new ParseException("Expected [/VOICES] to close voices block", Peek().Position);
            }
            
            return voices;
        }

        /// <summary>
        /// 解析音符
        /// </summary>
        /// <returns>音符节点</returns>
        private NoteNode ParseNote()
        {
            Token noteToken = Advance(); // 音名
            
            NoteNode note = new NoteNode
            {
                NoteName = noteToken.Lexeme,
                Position = noteToken.Position
            };
            
            // 解析八度和时值
            if (Match(TokenType.NUMBER))
            {
                // 这里简化处理，假设数字是八度
                string octaveText = Previous().Lexeme;
                if (int.TryParse(octaveText, out int octave))
                {
                    note.Octave = octave;
                }
            }
            
            // 检查是否有倚音（在时值之前）

            if (Match(TokenType.BACKSLASH))

            {

                // 解析倚音

                note.GraceNote = ParseGraceNote();

            }

            

            // 检查是否有滑音（在时值之前）

            if (Match(TokenType.GLISSANDO))

            {

                // 解析滑音起始音符

                note.GlissandoStart = ParseGlissandoStartNote();

            }
            
            // 解析时值（在括号中）

            if (Match(TokenType.LEFT_PAREN))

            {

                if (Match(TokenType.NUMBER))

                {

                    note.Duration = Previous().Lexeme;

                }

                

                // 检查是否有附点

                if (Match(TokenType.DOT))

                {

                    note.IsDotted = true;

                }

                

                // 检查是否有颤音标记（在时值之后）

                if (Match(TokenType.TILDE))

                {

                    note.HasVibrato = true;

                }

                

                // 期望右括号

                if (!Match(TokenType.RIGHT_PAREN))

                {

                    throw new ParseException("Expected ')' after duration", Peek().Position);

                }

            }

            

            // 检查是否有连音线标记（在括号之后）

            if (Match(TokenType.TIE))

            {

                // 连音线标记存在，将在后续处理中使用

                // 实际的连音线处理将在解析完整个小节后进行

            }
            
            return note;
        }

        /// <summary>
        /// 解析倚音
        /// </summary>
        /// <returns>倚音节点</returns>
        private NoteNode ParseGraceNote()
        {
            // 倚音格式：\C4 或 \D5 等
            if (!Check(TokenType.NOTE_NAME))
            {
                throw new ParseException("Expected note name after grace note marker", Peek().Position);
            }

            Token noteToken = Advance(); // 音名
            
            NoteNode graceNote = new NoteNode
            {
                NoteName = noteToken.Lexeme,
                Position = noteToken.Position
            };
            
            // 解析八度
            if (Match(TokenType.NUMBER))
            {
                // 这里简化处理，假设数字是八度
                string octaveText = Previous().Lexeme;
                if (int.TryParse(octaveText, out int octave))
                {
                    graceNote.Octave = octave;
                }
            }
            
            return graceNote;
        }

        /// <summary>
        /// 解析滑音起始音符
        /// </summary>
        /// <returns>滑音起始音符节点</returns>
        private NoteNode ParseGlissandoStartNote()
        {
            // 滑音格式：\\C4 G4(1/2) 等
            if (!Check(TokenType.NOTE_NAME))
            {
                throw new ParseException("Expected note name after glissando marker", Peek().Position);
            }

            Token noteToken = Advance(); // 音名
            
            NoteNode glissandoStartNote = new NoteNode
            {
                NoteName = noteToken.Lexeme,
                Position = noteToken.Position
            };
            
            // 解析八度
            if (Match(TokenType.NUMBER))
            {
                // 这里简化处理，假设数字是八度
                string octaveText = Previous().Lexeme;
                if (int.TryParse(octaveText, out int octave))
                {
                    glissandoStartNote.Octave = octave;
                }
            }
            
            return glissandoStartNote;
        }

        /// <summary>
        /// 解析休止符
        /// </summary>
        /// <returns>休止符节点</returns>
        private RestNode ParseRest()
        {
            Token restToken = Advance(); // R
            
            RestNode rest = new RestNode
            {
                Position = restToken.Position
            };
            
            // 解析时值（在括号中）
            if (Match(TokenType.LEFT_PAREN))
            {
                if (Match(TokenType.NUMBER))
                {
                    rest.Duration = Previous().Lexeme;
                }
                
                // 检查是否有附点
                if (Match(TokenType.DOT))
                {
                    rest.IsDotted = true;
                }
                
                // 期望右括号
                if (!Match(TokenType.RIGHT_PAREN))
                {
                    throw new ParseException("Expected ')' after rest duration", Peek().Position);
                }
            }
            
            return rest;
        }

        /// <summary>
        /// 解析和弦
        /// </summary>
        /// <returns>和弦节点</returns>
        private ChordNode ParseChord()
        {
            ChordNode chord = new ChordNode
            {
                Position = Previous().Position  // 使用Previous而不是Peek，因为我们已经消耗了左大括号
            };

            // 解析和弦中的音符
            do
            {
                // 跳过可能的空格或其他无关字符
                while (!IsAtEnd() && !Check(TokenType.NOTE_NAME) && !Check(TokenType.RIGHT_BRACE))
                {
                    Advance();
                }
                
                // 检查是否到达右大括号
                if (Check(TokenType.RIGHT_BRACE))
                {
                    break;
                }
                
                // 解析音符名称
                if (!Check(TokenType.NOTE_NAME))
                {
                    throw new ParseException("Expected note name in chord", Peek().Position);
                }

                Token noteToken = Advance();
                NoteNode note = new NoteNode
                {
                    NoteName = noteToken.Lexeme,
                    Position = noteToken.Position
                };

                // 解析八度
                if (Match(TokenType.NUMBER))
                {
                    string octaveText = Previous().Lexeme;
                    if (int.TryParse(octaveText, out int octave))
                    {
                        note.Octave = octave;
                    }
                }

                chord.Notes.Add(note);

                // 跳过可能的空格或其他无关字符
                while (!IsAtEnd() && !Check(TokenType.COMMA) && !Check(TokenType.RIGHT_BRACE))
                {
                    Advance();
                }
                
                // 如果是逗号，继续解析下一个音符
            } while (Match(TokenType.COMMA));

            // 期望右大括号
            if (!Match(TokenType.RIGHT_BRACE))
            {
                throw new ParseException("Expected '}' after chord notes", Peek().Position);
            }

            // 解析时值（在括号中）
            if (Match(TokenType.LEFT_PAREN))
            {
                if (Match(TokenType.NUMBER))
                {
                    chord.Duration = Previous().Lexeme;
                }

                // 检查是否有附点
                if (Match(TokenType.DOT))
                {
                    chord.IsDotted = true;
                }

                // 期望右括号
                if (!Match(TokenType.RIGHT_PAREN))
                {
                    throw new ParseException("Expected ')' after chord duration", Peek().Position);
                }
            }

            return chord;
        }

        /// <summary>
        /// 检查当前词法单元是否匹配指定类型（不消耗）
        /// </summary>
        /// <param name="type">词法单元类型</param>
        /// <returns>是否匹配</returns>
        private bool Check(TokenType type)
        {
            if (IsAtEnd()) return false;
            return Peek().Type == type;
        }

        /// <summary>
        /// 检查并消耗当前词法单元（如果匹配指定类型）
        /// </summary>
        /// <param name="type">词法单元类型</param>
        /// <returns>是否匹配并消耗</returns>
        private bool Match(TokenType type)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取当前词法单元（不消耗）
        /// </summary>
        /// <returns>当前词法单元</returns>
        private Token Peek()
        {
            return _tokens[_current];
        }

        /// <summary>
        /// 获取前一个词法单元
        /// </summary>
        /// <returns>前一个词法单元</returns>
        private Token Previous()
        {
            return _tokens[_current - 1];
        }

        /// <summary>
        /// 消耗当前词法单元并前进到下一个
        /// </summary>
        /// <returns>当前词法单元</returns>
        private Token Advance()
        {
            if (!IsAtEnd()) _current++;
            return _tokens[_current - 1];
        }

        /// <summary>
        /// 判断是否到达词法单元流末尾
        /// </summary>
        /// <returns>是否到达末尾</returns>
        private bool IsAtEnd()
        {
            return Peek().Type == TokenType.EOF;
        }
    }

    /// <summary>
    /// 解析异常类
    /// </summary>
    public class ParseException : Exception
    {
        public int Position { get; }

        public ParseException(string message, int position) : base(message)
        {
            Position = position;
        }
    }
}