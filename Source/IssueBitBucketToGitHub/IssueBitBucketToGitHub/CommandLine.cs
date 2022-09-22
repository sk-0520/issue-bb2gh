using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ContentTypeTextNet.Pe.Core.Models
{
    /// <summary>
    /// コマンドラインのキー。
    /// </summary>
    public record CommandLineKey
    {
        #region define

        /// <summary>
        /// 未設定の短い形式。
        /// </summary>
        public const char EmptyShortKey = '\0';

        #endregion

        /// <summary>
        /// 生成。
        /// </summary>
        /// <param name="shortKey">短いキー。</param>
        /// <param name="longKey">長いキー。</param>
        /// <param name="hasValue">値を持つか。</param>
        /// <param name="description">説明。</param>
        public CommandLineKey(char shortKey, string longKey, bool hasValue, string description)
        {
            ShortKey = shortKey;
            LongKey = TextUtility.SafeTrim(longKey);
            HasValue = hasValue;
            Description = description;
        }

        #region property

        /// <summary>
        /// 短いキー。
        /// <para>一文字。</para>
        /// </summary>
        public char ShortKey { get; }
        /// <summary>
        /// 長いキー。
        /// <para><see cref="ShortKey"/>の長ーい文字列。</para>
        /// </summary>
        public string LongKey { get; }
        /// <summary>
        /// 値を持つか。
        /// </summary>
        public bool HasValue { get; }
        /// <summary>
        /// コマンドラインの説明。
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 有効な<see cref="ShortKey"/>か。
        /// </summary>
        public bool IsEnabledShortKey => ShortKey != EmptyShortKey;
        /// <summary>
        /// 有効な<see cref="LongKey"/>か。
        /// </summary>
        public bool IsEnabledLongKey => !string.IsNullOrEmpty(LongKey);

        #endregion
    }

    /// <summary>
    /// コマンドラインの値。
    /// </summary>
    public interface ICommandLineValue
    {
        #region property

        /// <summary>
        /// 値一覧。
        /// </summary>
        IReadOnlyList<string> Items { get; }
        /// <summary>
        /// 最初の値。
        /// </summary>
        string First { get; }

        #endregion
    }

    /// <summary>
    /// コマンドラインの値。
    /// </summary>
    public class CommandLineValue: ICommandLineValue
    {
        #region ICommandLineValue

        /// <inheritdoc cref="ICommandLineValue.Items"/>
        public List<string> Items { get; } = new List<string>();
        IReadOnlyList<string> ICommandLineValue.Items => Items;

        /// <inheritdoc cref="ICommandLineValue.First"/>
        public string First => Items.First();

        #endregion

        #region function

        /// <summary>
        /// 値の追加。
        /// </summary>
        /// <param name="value"></param>
        public void Add(string value)
        {
            Items.Add(value);
        }

        #endregion
    }

    /// <summary>
    /// <para>/key value</para>
    /// <para>/key=value</para>
    /// <para>-key value</para>
    /// <para>-key=value</para>
    /// <para>--key value</para>
    /// <para>--key=value</para>
    /// <para>/switch</para>
    /// <para>-switch</para>
    /// <para>--switch</para>
    /// <para>短いキーをいっぱいくっつけてどうとかはできない。</para>
    /// </summary>
    public class CommandLine
    {
        /// <summary>
        /// アプリ状態から生成。
        /// <para><see cref="Environment.GetCommandLineArgs()"/>からコマンドライン分解。</para>
        /// <para><see cref="CommandName"/>を含む。</para>
        /// </summary>
        public CommandLine()
            : this(Environment.GetCommandLineArgs(), true)
        { }

        /// <summary>
        /// 指定引数から生成。
        /// </summary>
        /// <param name="arguments">コマンドライン引数。</param>
        /// <param name="withCommand"><see cref="arguments"/>の先頭はプログラム/コマンドか。<para>Main関数だと含まれず、<see cref="Environment.GetCommandLineArgs()"/>だと含まれてる的な。</para></param>
        public CommandLine(IEnumerable<string> arguments, bool withCommand = false)
        {
            if(withCommand) {
                CommandName = arguments.FirstOrDefault() ?? string.Empty;
                Arguments = arguments.Skip(1).ToList();
            } else {
                CommandName = string.Empty;
                Arguments = arguments.ToList();
            }
        }

        #region property

        /// <summary>
        /// プログラム/コマンド名。
        /// <para><c>null</c>が入ることはない。</para>
        /// </summary>
        public string CommandName { get; }
        /// <summary>
        /// プログラム名を含まないコマンドライン引数。
        /// </summary>
        public IReadOnlyList<string> Arguments { get; }

        /// <summary>
        /// 解析が完了したか。
        /// </summary>
        public bool IsParsed { get; private set; }

        /// <summary>
        /// キーアイテム一覧。
        /// </summary>
        private List<CommandLineKey> KeyItems { get; } = new List<CommandLineKey>();
        /// <summary>
        /// キーアイテム一覧。
        /// </summary>
        public IReadOnlyList<CommandLineKey> Keys => KeyItems;

        /// <summary>
        /// 値一覧実体。
        /// </summary>
        private Dictionary<CommandLineKey, ICommandLineValue> ValueItems { get; } = new Dictionary<CommandLineKey, ICommandLineValue>();
        /// <summary>
        /// 値一覧。
        /// </summary>
        public IReadOnlyDictionary<CommandLineKey, ICommandLineValue> Values => ValueItems;

        /// <summary>
        /// スイッチ一覧実体。
        /// </summary>
        private HashSet<CommandLineKey> SwitchItems { get; } = new HashSet<CommandLineKey>();
        /// <summary>
        /// スイッチ一覧。
        /// </summary>
        public IReadOnlyCollection<CommandLineKey> Switches => SwitchItems;

        /// <summary>
        /// 不明アイテム一覧実体。
        /// </summary>
        private List<string> UnknownItems { get; } = new List<string>();
        /// <summary>
        /// 不明アイテム一覧。
        /// </summary>
        public IReadOnlyList<string> Unknowns => UnknownItems;

        /// <summary>
        /// 解析時例外。
        /// </summary>
        public Exception? ParseException { get; private set; }

        #endregion

        #region function

        private CommandLineKey AddCore(CommandLineKey key)
        {
            KeyItems.Add(key);
            return key;
        }

        /// <summary>
        /// コマンドラインキーの追加。
        /// </summary>
        /// <param name="key">キー情報。</param>
        /// <returns>追加したキー。</returns>
        public CommandLineKey Add(CommandLineKey key)
        {
            if(key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if(!key.IsEnabledShortKey && !key.IsEnabledLongKey) {
                throw new ArgumentException($"{nameof(key.ShortKey)} and {nameof(key.LongKey)} is empty");
            }

            if(key.IsEnabledLongKey && key.LongKey.Length == 1) {
                throw new ArgumentException(nameof(key.LongKey));
            }

            if(KeyItems.Where(k => k.IsEnabledShortKey).Any(k => k.ShortKey == key.ShortKey)) {
                throw new ArgumentException($"exists {nameof(key.ShortKey)}: {key.ShortKey}");
            }

            if(KeyItems.Where(k => k.IsEnabledLongKey).Any(k => k.LongKey == key.LongKey)) {
                throw new ArgumentException($"exists {nameof(key.LongKey)}: {key.LongKey}");
            }

            return AddCore(key);
        }

        /// <summary>
        /// コマンドラインキーの追加。
        /// </summary>
        /// <param name="shortKey">短いキー。</param>
        /// <param name="longKey">長いキー。</param>
        /// <param name="hasValue">値を持つか。</param>
        /// <param name="description">説明。</param>
        /// <returns>追加したキー。</returns>
        public CommandLineKey Add(char shortKey = CommandLineKey.EmptyShortKey, string longKey = "", bool hasValue = false, string description = "")
        {
            var value = new CommandLineKey(shortKey, longKey, hasValue, description);
            return Add(value);
        }

        string StripDoubleQuotes(string s)
        {
            if(s.Length > "\"\"".Length && s[0] == '"' && s[^1] == '"') {
                return s.Substring(1, s.Length - 1 - 1);
            }

            return s;
        }

        private CommandLineKey? GetCommandLineKey(string key)
        {
            Debug.Assert(key.Length != 0);

            if(key.Length == 1) {
                return KeyItems.Find(k => k.IsEnabledShortKey && k.ShortKey == key[0]);
            } else if(1 < key.Length) {
                return KeyItems.Find(k => k.IsEnabledLongKey && k.LongKey == key);
            }

            Debug.Assert(false);
            throw new NotImplementedException();
        }

        private void SetValue(CommandLineKey key, string value)
        {
            if(ValueItems.TryGetValue(key, out var val)) {
                ((CommandLineValue)val).Add(value);
            } else {
                var commandLineValue = new CommandLineValue();
                commandLineValue.Add(value);
                ValueItems.Add(key, commandLineValue);
            }
        }

        private void SetSwitch(CommandLineKey value)
        {
            SwitchItems.Add(value);
        }

        private void SetUnknown(string value)
        {
            UnknownItems.Add(value);
        }

        private bool ParseCore()
        {
            try {
                var map = new[] {
                    new { Key = "--", IsLong = true },
                    new { Key = "-",  IsLong = false },
                    new { Key = "/",  IsLong = false },
                };

                for(var i = 0; i < Arguments.Count; i++) {
                    var argument = Arguments[i];
                    var arg = StripDoubleQuotes(argument);
                    if(string.IsNullOrWhiteSpace(arg)) {
                        continue;
                    }

                    var pair = map.FirstOrDefault(i => arg.StartsWith(i.Key));
                    if(pair != null) {
                        var separatorIndex = arg.IndexOf('=');
                        if(separatorIndex == -1) {
                            var key = GetCommandLineKey(arg.Substring(pair.Key.Length));
                            if(key == null) {
                                SetUnknown(arg);
                                continue;
                            }
                            if(key.HasValue) {
                                if(i < Arguments.Count - 1) {
                                    SetValue(key, Arguments[i + 1]);
                                    i += 1;
                                    continue;
                                } else {
                                    SetValue(key, string.Empty);
                                    continue;
                                }
                            } else {
                                SetSwitch(key);
                                continue;
                            }
                        } else {
                            var key = GetCommandLineKey(arg.Substring(pair.Key.Length, separatorIndex - pair.Key.Length));
                            if(key == null) {
                                SetUnknown(arg);
                                continue;
                            }
                            if(key.HasValue) {
                                var val = arg.Substring(separatorIndex + 1);
                                SetValue(key, StripDoubleQuotes(val));
                                continue;
                            } else {
                                SetSwitch(key);
                                continue;
                            }
                        }
                    } else {
                        SetUnknown(arg);
                    }
                }
                return true;
            } catch(Exception ex) {
                ParseException = ex;
                return false;
            }
        }

        /// <summary>
        /// 解析処理実行。
        /// </summary>
        /// <returns></returns>
        public bool Parse()
        {
            if(IsParsed) {
                throw new InvalidOperationException();
            }

            var result = ParseCore();
            IsParsed = true;

            return result;
        }

        /// <summary>
        /// 短いキーから値取得。
        /// </summary>
        /// <param name="shortKey">短いキー。</param>
        /// <returns>取得した値。取得できない場合は<c>null</c>。</returns>
        public CommandLineKey? GetKey(char shortKey)
        {
            return KeyItems
                .Concat(SwitchItems)
                .Where(k => k.IsEnabledShortKey)
                .FirstOrDefault(k => k.ShortKey == shortKey)
            ;
        }

        /// <summary>
        /// 長いキーから値取得。
        /// </summary>
        /// <param name="longKey">長いキー。</param>
        /// <returns>取得した値。取得できない場合は<c>null</c>。</returns>
        public CommandLineKey? GetKey(string longKey)
        {
            return KeyItems
                .Concat(SwitchItems)
                .Where(k => k.IsEnabledLongKey)
                .FirstOrDefault(k => k.LongKey == longKey)
            ;
        }

        /// <summary>
        /// 文字列をコマンド実行可能な書式に変換する。
        /// </summary>
        /// <param name="input">対象文字列。</param>
        /// <returns></returns>
        public static string Escape(string input)
        {
            if(string.IsNullOrWhiteSpace(input)) {
                return string.Empty;
            }

            var s = input.Trim();
            var result = s.Replace("\"", "\"\"");
            if(s.IndexOf(' ') == -1) {
                return result;
            } else {
                return "\"" + result + "\"";
            }
        }

        #endregion
    }

    public static class CommandLineExtensions
    {
        #region function

        /// <summary>
        /// <see cref="CommandLine"/>のキーから値を取得。
        /// </summary>
        /// <param name="commandLine">対象の<see cref="CommandLine"/>。</param>
        /// <param name="key">キー。</param>
        /// <param name="defaultValue"><paramref name="key"/>が存在しない場合の戻り値。</param>
        /// <returns><paramref name="key"/>に対する値。存在しない場合は<paramref name="defaultValue"/>を返す。</returns>
        public static string GetValue(this CommandLine commandLine, string key, string defaultValue)
        {
            var commandLineKey = commandLine.GetKey(key);
            if(commandLineKey != null) {
                if(commandLine.Values.TryGetValue(commandLineKey, out var value)) {
                    return value.First;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// <see cref="CommandLine"/>のスイッチを取得。
        /// </summary>
        /// <param name="commandLine">対象の<see cref="CommandLine"/>。</param>
        /// <param name="key">キー。</param>
        /// <returns>スイッチ状態。</returns>
        public static bool ExistsSwitch(this CommandLine commandLine, string key)
        {
            var commandLineKey = commandLine.GetKey(key);
            if(commandLineKey != null) {
                return commandLine.Switches.Contains(commandLineKey);
            }

            return false;
        }

        #endregion
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class CommandLineAttribute: Attribute
    {
        public CommandLineAttribute(char shortKey = CommandLineKey.EmptyShortKey, string longKey = "", string description = "", bool hasValue = true)
        {
            ShortKey = shortKey;
            LongKey = longKey;
            Description = description;
            HasValue = hasValue;
        }

        #region property

        /// <summary>
        /// 短いキー。
        /// <para>一文字。</para>
        /// </summary>
        public char ShortKey { get; }
        /// <summary>
        /// 長いキー。
        /// <para><see cref="ShortKey"/>の長ーい文字列。</para>
        /// </summary>
        public string LongKey { get; }
        /// <summary>
        /// 値を持つか。
        /// </summary>
        public bool HasValue { get; }
        /// <summary>
        /// コマンドラインの説明。
        /// </summary>
        public string Description { get; }

        #endregion
    }

    /// <summary>
    /// コマンドラインをデータ構造にマッピング。
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class CommandLineConverter<TData>
        where TData : class
    {
        public CommandLineConverter(CommandLine commandLine, TData data)
        {
            CommandLine = commandLine;
            Data = data;
        }

        #region property

        public CommandLine CommandLine { get; }
        public TData Data { get; }
        public Exception? Exception { get; private set; }

        #endregion

        #region function

        private IReadOnlyDictionary<PropertyInfo, CommandLineAttribute> GetPropertyAttributeMapping(Type type)
        {
            var properties = type.GetProperties();

            var map = new Dictionary<PropertyInfo, CommandLineAttribute>(properties.Length);
            foreach(var property in properties) {
                var attributes = property.GetCustomAttributes(typeof(CommandLineAttribute), true);
                if(attributes != null && attributes.Any()) {
                    map.Add(property, attributes.OfType<CommandLineAttribute>().First());
                }
            }

            return map;
        }

        private IReadOnlyDictionary<PropertyInfo, CommandLineKey> SetPropertyKeyMapping(CommandLine commandLine, IReadOnlyDictionary<PropertyInfo, CommandLineAttribute> propertyAttributeMap)
        {
            var map = new Dictionary<PropertyInfo, CommandLineKey>();
            foreach(var pair in propertyAttributeMap) {
                var attribute = pair.Value;
                var key = commandLine.Add(attribute.ShortKey, attribute.LongKey, attribute.HasValue, attribute.Description);
                map.Add(pair.Key, key);
            }

            return map;
        }

        object? ConvertValue(Type type, string rawValue)
        {
            if(type == typeof(float)) {
                return float.Parse(rawValue);
            }
            if(type == typeof(double)) {
                return double.Parse(rawValue);
            }

            return Convert.ChangeType(rawValue, type);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "HAA0601:Value type to reference type conversion causing boxing allocation")]
        private object GetTrueSwitch(Type type)
        {
            if(type == typeof(bool)) {
                return true;
            }

            if(type == typeof(char)) {
                return 'Y';
            }

            if(type == typeof(string)) {
                return true.ToString();
            }

            var numTypes = new[] {
                typeof(sbyte),
                typeof(byte),
                typeof(short),
                typeof(ushort),
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong),
                typeof(float),
                typeof(double),
            };

            if(Array.IndexOf(numTypes, type) != -1) {
                return 1;
            }

            if(type == typeof(decimal)) {
                return 1m;
            }

            throw new NotImplementedException();
        }

        protected bool MappingCore()
        {
            var type = Data.GetType();
            var attributeMap = GetPropertyAttributeMapping(type);

            var keyMap = SetPropertyKeyMapping(CommandLine, attributeMap);
            try {
                if(CommandLine.Parse()) {
                    foreach(var pair in keyMap) {
                        if(pair.Value.HasValue) {
                            // 値取得
                            if(CommandLine.Values.TryGetValue(pair.Value, out var value)) {
                                var convertedValue = ConvertValue(pair.Key.PropertyType, value.First);
                                pair.Key.SetValue(Data, convertedValue);
                            }
                        } else {
                            // スイッチ
                            if(CommandLine.Switches.Contains(pair.Value)) {
                                var switchValue = GetTrueSwitch(pair.Key.PropertyType);
                                pair.Key.SetValue(Data, switchValue);
                            }
                        }
                    }
                }

                return true;
            } catch(Exception ex) {
                Exception = ex;
                return false;
            }
        }

        public virtual bool Mapping()
        {
            return MappingCore();
        }

        #endregion
    }

    sealed public class CommandLineSimpleConverter<TData>: CommandLineConverter<TData>
        where TData : class, new()
    {
        public CommandLineSimpleConverter(CommandLine commandLine)
            : base(commandLine, new TData())
        { }

        #region function

        public TData? GetMappingData()
        {
            var result = MappingCore();
            if(!result) {
                return default;
            }

            return Data;
        }


        #endregion

        #region CommandLineConverter

        public override bool Mapping()
        {
            throw new NotSupportedException();
        }

        #endregion
    }

    public static class CommandLineDictionaryExtensions
    {
        #region function

        /// <summary>
        /// <see cref="IDictionary{TKey, TValue}"/>をいい感じにつなげる。
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        public static IEnumerable<string> ToCommandLineArguments(this IDictionary<string, string> map, string header = "--", char separator = '=')
        {
            return map.Select(i => header + i.Key + separator + CommandLine.Escape(i.Value));
        }

        #endregion
    }
}
