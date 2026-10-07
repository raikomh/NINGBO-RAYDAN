using BusinessSearcher.Domain.Common;
using BusinessSearcher.Domain.Exceptions;

namespace BusinessSearcher.Domain.BoundedContext.Radar.Aggregates
{
    /// <summary>
    /// Pregunta de la comunidad (feature 15): "¿Alguien ha visto leche en el Vedado?".
    /// Otros clientes responden rápidamente.
    /// </summary>
    public class CommunityQuestion : Entity, IAggregateRoot
    {
        public Guid    ClientId    { get; private set; }
        public string  AskerName   { get; private set; } = default!;
        public string  Text        { get; private set; } = default!;
        public string? City        { get; private set; }
        public string? ProductName { get; private set; }

        private readonly List<CommunityAnswer> _answers = new();
        public IReadOnlyCollection<CommunityAnswer> Answers => _answers.AsReadOnly();

        private CommunityQuestion() { }

        public static CommunityQuestion Create(Guid clientId, string askerName, string text, string? city, string? productName)
        {
            if (clientId == Guid.Empty)
                throw new DomainException("La pregunta debe tener un autor.");
            if (string.IsNullOrWhiteSpace(text))
                throw new DomainException("La pregunta no puede estar vacía.");
            if (text.Trim().Length > 500)
                throw new DomainException("La pregunta no puede exceder 500 caracteres.");

            return new CommunityQuestion
            {
                ClientId    = clientId,
                AskerName   = string.IsNullOrWhiteSpace(askerName) ? "Anónimo" : askerName.Trim(),
                Text        = text.Trim(),
                City        = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
                ProductName = string.IsNullOrWhiteSpace(productName) ? null : productName.Trim()
            };
        }

        public CommunityAnswer AddAnswer(Guid clientId, string answererName, string text)
        {
            if (clientId == Guid.Empty)
                throw new DomainException("La respuesta debe tener un autor.");
            if (string.IsNullOrWhiteSpace(text))
                throw new DomainException("La respuesta no puede estar vacía.");

            var answer = new CommunityAnswer(Id, clientId,
                string.IsNullOrWhiteSpace(answererName) ? "Anónimo" : answererName.Trim(), text.Trim());
            _answers.Add(answer);
            SetUpdated();
            return answer;
        }
    }

    public class CommunityAnswer : Entity
    {
        public Guid   QuestionId   { get; private set; }
        public Guid   ClientId     { get; private set; }
        public string AnswererName { get; private set; } = default!;
        public string Text         { get; private set; } = default!;

        private CommunityAnswer() { }

        internal CommunityAnswer(Guid questionId, Guid clientId, string answererName, string text)
        {
            QuestionId   = questionId;
            ClientId     = clientId;
            AnswererName = answererName;
            Text         = text.Length > 500 ? text[..500] : text;
        }
    }
}
