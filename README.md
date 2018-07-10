# gziptest

Для сборки требуется [.NET Core 2.1 SDK](https://www.microsoft.com/net/download/thank-you/dotnet-sdk-2.1.301-windows-x64-installer).

Исполняемый проект называется GzipTest (target framework = 471).  
В проекте Parallel.Compression находится основной код.  
В проекте Parallel.Compression.Cli находится код консольного интерфейса.  
Проект Parallel.Compression.Console используется для запуска в виде dotnet core консольного приложения.

В отличии от сжатия, для многопоточной блочной декомпресии с помощью GzipStream нужны различные ухищерения. В данном проекте используется три способа:

1. При компрессии в заголовок каждого блока, в секцию Mime Type (она не используется GzipStream), устанавливается длина сжатого блока. Если при декомпрессии окажется, что длина блока слишком велика для декомпрессии, то такой способ отклоняется. Также проверяется, что длина имеет корректное значение. Далее прочитанный блок передается в WorkerPool для декомпресии.

2. Если первым способом начать разархивирование не удалось, то производится попытка разархивирования буфферами заданного размера. В прочитанном буфере определяется текущий заголовок и расположение следующего. Если блок однозначно не может быть выделен в прочитанном буффере, то данный способ отклоняется. Также как и первом способе, каждый прочитанный блок разархивируется в отдельном потоке.

3. Если ни один из предыдущих способов не подходит, то выбирается способ однопточной декомрпессии с помощью Stream'ов. В этом случае входной stream оборачивается в stream, который умеет определять границы Gzip блоков в буферах запрашиваемых GzipStream при разархивировании. Таким образом, можно разархивировать или слишком большие блоки, или, когда весь архив представлен одним Gzip-блоком большого размера.
