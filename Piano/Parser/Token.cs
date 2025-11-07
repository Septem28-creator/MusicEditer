using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piano.Parser
{
    /// <summary>
    /// 词法单元类型枚举
    /// </summary>
    public enum TokenType
    {
        // 音名
        NOTE_NAME,      // C, D, E, F, G, A, B
        SHARP,          // #
        FLAT,           // b
        
        // 数字和符号
        NUMBER,         // 数字
        DOT,            // 附点
        LEFT_PAREN,     // (
        RIGHT_PAREN,    // )
        LEFT_BRACE,     // {
        RIGHT_BRACE,    // }
        COMMA,          // ,
        COLON,          // :
        SLASH,          // /
        BACKSLASH,      // \ (倚音)
        
        // 特殊符号
        REST,           // R (休止符)
        BAR,            // | (小节线)
        TILDE,          // ~ (连音线和颤音)
        TIE,            // - (连音线)
        GLISSANDO,      // \ (滑音)
        
        // 标识符
        IDENTIFIER,     // 标识符
        VOICE,          // V (声部标记)
        
        // 关键字
        BPM,           // BPM
        KEY,           // KEY
        INTRO,         // INTRO
        INTERLUDE,     // INTERLUDE
        REPEAT,        // REPEAT
        END_REPEAT,    // /REPEAT
        PART,          // PART
        END_PART,      // /PART
        VOICE_BLOCK,   // VOICE
        END_VOICE,     // /VOICE
        VOICES,        // VOICES
        END_VOICES,    // /VOICES
        
        // 强弱记号
        DYNAMICS,      // p, pp, mp, mf, f, ff, fff
        
        // 文件结束
        EOF,           // 文件结束
        
        // 注释
        COMMENT,       // 注释
    }

    /// <summary>
    /// 词法单元类
    /// </summary>
    public class Token
    {
        /// <summary>
        /// 词法单元类型
        /// </summary>
        public TokenType Type { get; set; }

        /// <summary>
        /// 词素（在源代码中的实际文本）
        /// </summary>
        public string Lexeme { get; set; }

        /// <summary>
        /// 词法单元在源代码中的起始位置
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="type">词法单元类型</param>
        /// <param name="lexeme">词素</param>
        /// <param name="position">位置</param>
        public Token(TokenType type, string lexeme, int position)
        {
            Type = type;
            Lexeme = lexeme;
            Position = position;
        }

        public override string ToString()
        {
            return $"{Type} '{Lexeme}' at position {Position}";
        }
    }
}