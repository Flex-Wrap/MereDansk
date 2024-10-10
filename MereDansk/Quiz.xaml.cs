using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace MereDansk
{
    public partial class Quiz : ContentPage
    {
        // Constants
        private const int MaxQuestions = 10;
        private const string ScoreboardFileName = "scoreboard.txt";

        // Fields
        private List<QuestionModel> _questions = new List<QuestionModel>();
        private List<bool> _results = new List<bool>();
        private int _currentQuestionIndex = 0;

        // Constructor
        public Quiz()
        {
            InitializeComponent();
            LoadQuestions();
            LoadScoreboard();
        }

        // Load questions from file and display the first question
        private async void LoadQuestions()
        {
            _questions = await ReadQuestionsFromFileAsync("questions.txt");
            _results.Clear();
            DisplayCurrentQuestion();
        }

        // Read questions from an embedded resource file
        private async Task<List<QuestionModel>> ReadQuestionsFromFileAsync(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"MereDansk.Resources.Raw.{fileName}";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Resource '{resourceName}' not found.");
            }
            using StreamReader reader = new StreamReader(stream);
            var questions = new List<QuestionModel>();
            string? line;
            string? questionText = null;
            string? correctAnswer = null;
            var answers = new List<string>();

            // Read each line and parse questions and answers
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (questionText != null && correctAnswer != null && answers.Count > 0)
                    {
                        var question = new QuestionModel
                        {
                            Question = questionText,
                            CorrectAnswer = correctAnswer,
                            Answers = new List<string>(answers)
                        };
                        question.ShuffleAnswers();
                        questions.Add(question);
                    }

                    questionText = null;
                    correctAnswer = null;
                    answers.Clear();
                }
                else if (questionText == null)
                {
                    questionText = line;
                }
                else if (correctAnswer == null)
                {
                    correctAnswer = line;
                }
                else
                {
                    answers.Add(line);
                }
            }

            // Add the last question if the file does not end with a blank line
            if (questionText != null && correctAnswer != null && answers.Count > 0)
            {
                var question = new QuestionModel
                {
                    Question = questionText,
                    CorrectAnswer = correctAnswer,
                    Answers = new List<string>(answers)
                };
                question.ShuffleAnswers();
                questions.Add(question);
            }

            // Shuffle and take a subset of questions
            var shuffledQuestions = questions.OrderBy(q => Guid.NewGuid()).Take(MaxQuestions).ToList();
            return shuffledQuestions;
        }

        // Display the current question and its answers
        private void DisplayCurrentQuestion()
        {
            if (_currentQuestionIndex >= _questions.Count)
            {
                // No more questions, show results
                QuestionLabel.IsVisible = false;
                AnswersStackLayout.Children.Clear();

                CompletionLabel.IsVisible = true;
                var correctAnswers = _results.Count(r => r);
                var totalQuestions = _results.Count;
                ResultLabel.Text = $"You answered {correctAnswers} out of {totalQuestions} questions correctly.";
                ResultLabel.IsVisible = true;
                TryAgainButton.IsVisible = true;

                SaveScore(correctAnswers, totalQuestions);
                LoadScoreboard();

                return;
            }

            // Display the current question and its answers
            var currentQuestion = _questions[_currentQuestionIndex];
            QuestionLabel.Text = currentQuestion.Question;
            var answers = currentQuestion.ShuffledAnswers;

            AnswersStackLayout.Children.Clear();
            foreach (var answer in answers)
            {
                var button = new Button
                {
                    Text = answer,
                    Margin = new Thickness(0, 5),
                    BackgroundColor = Colors.LightGray, // Optional: Set a background color for better visibility
                    TextColor = Colors.Black // Optional: Set text color for better visibility
                };
                button.Clicked += OnOptionClicked; // Ensure the Clicked event is properly assigned
                AnswersStackLayout.Children.Add(button);

                // Debugging output
                Console.WriteLine($"Button created: {answer}");
            }

            CompletionLabel.IsVisible = false;
            ResultLabel.IsVisible = false;
            TryAgainButton.IsVisible = false;
            QuestionLabel.IsVisible = true;
        }

        // Handle answer button click
        private void OnOptionClicked(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                var selectedAnswer = button.Text;
                var currentQuestion = _questions[_currentQuestionIndex];
                var isCorrect = selectedAnswer == currentQuestion.CorrectAnswer;
                _results.Add(isCorrect);

                // Debugging output
                Console.WriteLine($"Button clicked: {selectedAnswer}, Anwser is: {isCorrect}");
            }
            else
            {
                // Debugging output
                Console.WriteLine("OnOptionClicked: sender is not a Button");
            }

            _currentQuestionIndex++;
            DisplayCurrentQuestion();
        }

        // Handle "Try Again" button click
        private void OnTryAgainClicked(object? sender, EventArgs e)
        {
            _currentQuestionIndex = 0;
            LoadQuestions();
        }

        // Check if the file can be written to
        private bool CanWriteToFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);

                // Check if the file is read-only
                if (fileInfo.IsReadOnly)
                {
                    return false;
                }

                // Attempt to open the file with write access
                using (FileStream fs = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.Write))
                {
                    // If we can open the file for writing, we have write permissions
                    return true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // If an UnauthorizedAccessException is thrown, we do not have write permissions
                return false;
            }
            catch (Exception)
            {
                // Handle other potential exceptions
                return false;
            }
        }

        // Save the score to the scoreboard file
        private void SaveScore(int correctAnswers, int totalQuestions)
        {
            if (!CanWriteToFile(ScoreboardFileName))
            {
                // Handle the case where the file is not writable
                Console.WriteLine("Cannot write to the file. Check file permissions.");
                return;
            }

            var score = new ScoreModel
            {
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Score = $"{correctAnswers}/{totalQuestions}"
            };

            var scores = LoadScores();
            scores.Add(score);
            var scoresText = string.Join(Environment.NewLine, scores.Select(s => $"{s.Date},{s.Time},{s.Score}"));
            File.WriteAllText(ScoreboardFileName, scoresText);
        }

        // Load scores from the scoreboard file
        private List<ScoreModel> LoadScores()
        {
            var scores = new List<ScoreModel>();
            if (File.Exists(ScoreboardFileName))
            {
                var lines = File.ReadAllLines(ScoreboardFileName);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length == 3)
                    {
                        scores.Add(new ScoreModel
                        {
                            Date = parts[0],
                            Time = parts[1],
                            Score = parts[2]
                        });
                    }
                }
            }
            return scores;
        }

        // Load the scoreboard and display it
        private void LoadScoreboard()
        {
            var scores = LoadScores();
            ScoreboardCollectionView.ItemsSource = scores.OrderByDescending(s => s.Date).ThenByDescending(s => s.Time).ToList();
            ScoreboardCollectionView.IsVisible = true;
        }
    }

    // Model for a quiz question
    public class QuestionModel
    {
        public string Question { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public List<string> Answers { get; set; } = new List<string>();
        public List<string> ShuffledAnswers { get; private set; } = new List<string>();

        // Shuffle the answers and insert the correct answer at a random position
        public void ShuffleAnswers()
        {
            ShuffledAnswers = Answers.OrderBy(a => Guid.NewGuid()).ToList();
            ShuffledAnswers.Insert(new Random().Next(0, ShuffledAnswers.Count), CorrectAnswer);
        }
    }

    // Model for a score entry
    public class ScoreModel
    {
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty;
    }
}
