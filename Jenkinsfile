pipeline {
    agent none

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        NUGET_XMLDOC_MODE = 'skip'
    }

    stages {
        stage('Build') {
            agent { label 'docker' }
            steps {
                // SampleShared must be built first — all other projects reference its compiled DLL
                sh 'dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug'

                sh '''
                    dotnet build "Source/Samples/LiteDb/Samples.sln" -c Debug
                    dotnet build "Source/Samples/PostgreSQL/Samples.sln" -c Debug
                    dotnet build "Source/Samples/Redis/Samples.sln" -c Debug
                    dotnet build "Source/Samples/SQLite/Samples.sln" -c Debug
                    dotnet build "Source/Samples/SQLServer/Samples.sln" -c Debug
                    dotnet build "Source/Samples/DashBoard.Api/DashBoard.Api.sln" -c Debug
                    dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug
                '''
            }
        }

        stage('CI Integration Tests') {
            agent { label 'docker' }
            steps {
                sh 'dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug'
                sh 'dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug'

                sh '''
                    dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" \
                        -c Debug --no-build \
                        --filter "TestCategory=CI" \
                        -f net10.0
                '''
            }
        }

        stage('LocalOnly Integration Tests') {
            parallel {
                stage('PostgreSQL') {
                    agent { label 'docker' }
                    steps {
                        sh 'dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug'
                        sh 'dotnet build "Source/Samples/PostgreSQL/Samples.sln" -c Debug'
                        sh 'dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug'

                        withCredentials([string(credentialsId: 'postgresql-connstring', variable: 'POSTGRESQL_CONN')]) {
                            sh '''
                                sed -i "s|key=\\"Database\\" value=\\"[^\\"]*\\"|key=\\"Database\\" value=\\"${POSTGRESQL_CONN}\\"|" \
                                    "Source/Samples/PostgreSQL/PostgreSQLProducer/App.config"
                            '''
                        }

                        sh '''
                            dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" \
                                -c Debug --no-build \
                                --filter "FullyQualifiedName~PostgreSql" \
                                -f net10.0
                        '''
                    }
                }

                stage('SQL Server') {
                    agent { label 'docker' }
                    steps {
                        sh 'dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug'
                        sh 'dotnet build "Source/Samples/SQLServer/Samples.sln" -c Debug'
                        sh 'dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug'

                        withCredentials([string(credentialsId: 'sqlserver-connstring', variable: 'SQLSERVER_CONN')]) {
                            sh '''
                                sed -i "s|key=\\"Database\\" value=\\"[^\\"]*\\"|key=\\"Database\\" value=\\"${SQLSERVER_CONN}\\"|" \
                                    "Source/Samples/SQLServer/SQLServerProducer/App.config"
                            '''
                        }

                        sh '''
                            dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" \
                                -c Debug --no-build \
                                --filter "FullyQualifiedName~SqlServer" \
                                -f net10.0
                        '''
                    }
                }

                stage('Redis') {
                    agent { label 'docker' }
                    steps {
                        sh 'dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug'
                        sh 'dotnet build "Source/Samples/Redis/Samples.sln" -c Debug'
                        sh 'dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug'

                        withCredentials([string(credentialsId: 'redis-connstring', variable: 'REDIS_CONN')]) {
                            sh '''
                                sed -i "s|key=\\"Database\\" value=\\"[^\\"]*\\"|key=\\"Database\\" value=\\"${REDIS_CONN}\\"|" \
                                    "Source/Samples/Redis/RedisProducer/App.config"
                            '''
                        }

                        sh '''
                            dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" \
                                -c Debug --no-build \
                                --filter "FullyQualifiedName~Redis" \
                                -f net10.0
                        '''
                    }
                }
            }
        }
    }

    post {
        failure {
            echo 'Pipeline failed. Check stage logs for details.'
        }
        success {
            echo 'Pipeline completed successfully.'
        }
    }
}
