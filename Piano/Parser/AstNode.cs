using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piano.Parser
{
    /// <summary>
    /// 抽象语法树节点基类
    /// </summary>
    public abstract class AstNode
    {
        public int Position { get; set; }
    }

    /// <summary>
    /// 音符节点
    /// </summary>
    public class NoteNode : AstNode
    {
        public string NoteName { get; set; } = string.Empty;  // 音名 (C, D, E, F, G, A, B)
        public int Octave { get; set; }       // 八度 (4, 5, etc.)
        public string Duration { get; set; } = string.Empty;  // 时值 (1/4, 1/8, etc.)
        public bool IsDotted { get; set; }    // 是否附点
        public string Dynamics { get; set; } = string.Empty;  // 强弱记号 (p, f, mf, etc.)
        public int Voice { get; set; } = 0;   // 声部编号，默认为0
        public bool HasVibrato { get; set; } = false;  // 是否有颤音效果
        public NoteNode? GraceNote { get; set; } = null;  // 倚音
        public NoteNode? GlissandoStart { get; set; } = null;  // 滑音起始音符
    }

    /// <summary>
    /// 声部节点
    /// </summary>
    public class VoiceNode : AstNode
    {
        public int VoiceNumber { get; set; }  // 声部编号
        public List<MeasureNode> Measures { get; set; } = new List<MeasureNode>();  // 声部中的小节
    }

    /// <summary>
    /// 和弦节点
    /// </summary>
    public class ChordNode : AstNode
    {
        public List<NoteNode> Notes { get; set; } = new List<NoteNode>();  // 和弦中的音符列表
        public string Duration { get; set; } = string.Empty;  // 时值 (1/4, 1/8, etc.)
        public bool IsDotted { get; set; }    // 是否附点
        public string Dynamics { get; set; } = string.Empty;  // 强弱记号 (p, f, mf, etc.)
    }

    /// <summary>
    /// 休止符节点
    /// </summary>
    public class RestNode : AstNode
    {
        public string Duration { get; set; } = string.Empty;  // 时值 (1/4, 1/8, etc.)
        public bool IsDotted { get; set; }    // 是否附点
    }

    /// <summary>
    /// 连音线节点
    /// </summary>
    public class TieNode : AstNode
    {
        public NoteNode FirstNote { get; set; } = null!;
        public NoteNode SecondNote { get; set; } = null!;
    }

    /// <summary>
    /// 小节节点
    /// </summary>
    public class MeasureNode : AstNode
    {
        public List<AstNode> Elements { get; set; } = new List<AstNode>();
    }

    /// <summary>
    /// 速度标记节点
    /// </summary>
    public class TempoNode : AstNode
    {
        public int BPM { get; set; }  // 每分钟节拍数
    }

    /// <summary>
    /// 调号节点
    /// </summary>
    public class KeyNode : AstNode
    {
        public string Key { get; set; } = string.Empty;  // 调号 (C, Am, etc.)
    }

    /// <summary>
    /// 段落标记节点
    /// </summary>
    public class SectionNode : AstNode
    {
        public string SectionType { get; set; } = string.Empty;  // 段落类型 (INTRO, INTERLUDE, REPEAT)
        public List<MeasureNode> Measures { get; set; } = new List<MeasureNode>();
    }

    /// <summary>
    /// 音乐作品根节点
    /// </summary>
    public class ScoreNode : AstNode
    {
        public TempoNode? Tempo { get; set; }
        public KeyNode? Key { get; set; }
        public List<VoiceNode> Voices { get; set; } = new List<VoiceNode>();  // 声部列表
        public List<AstNode> Sections { get; set; } = new List<AstNode>();    // 段落列表
    }
}