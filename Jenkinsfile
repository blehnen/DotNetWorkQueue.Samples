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
                        -f net10.0 \
                        --logger "junit;LogFilePath=$WORKSPACE/junit-results/ci-{assembly}.{framework}.xml"
                '''
                stash includes: 'junit-results/**/*.xml', name: 'junit-ci', allowEmpty: true
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
                            // Every App.config a PostgreSql-filtered test reads must get the real
                            // connection string. The Outbox/Inbox tests read their own sample's
                            // App.config (see PostgreSqlOutboxTests / PostgreSqlInboxTests), not
                            // the Producer one, so they need injection too.
                            sh '''
                                for cfg in "Source/Samples/PostgreSQL/PostgreSQLProducer/App.config" "Source/Samples/PostgreSQL/PostgreSQLProducerOutbox/App.config" "Source/Samples/PostgreSQL/PostgreSQLConsumerInbox/App.config"; do
                                    sed -i "s|key=\\"Database\\" value=\\"[^\\"]*\\"|key=\\"Database\\" value=\\"${POSTGRESQL_CONN}\\"|" "$cfg"
                                done
                            '''
                        }

                        sh '''
                            dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" \
                                -c Debug --no-build \
                                --filter "FullyQualifiedName~PostgreSql" \
                                -f net10.0 \
                                --logger "junit;LogFilePath=$WORKSPACE/junit-results/postgresql-{assembly}.{framework}.xml"
                        '''
                        stash includes: 'junit-results/**/*.xml', name: 'junit-postgresql', allowEmpty: true
                    }
                }

                stage('SQL Server') {
                    agent { label 'docker' }
                    steps {
                        sh 'dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug'
                        sh 'dotnet build "Source/Samples/SQLServer/Samples.sln" -c Debug'
                        sh 'dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug'

                        withCredentials([string(credentialsId: 'sqlserver-connstring', variable: 'SQLSERVER_CONN')]) {
                            // See the PostgreSQL stage — the Outbox/Inbox tests read their own
                            // sample's App.config, so injection has to cover those too.
                            sh '''
                                for cfg in "Source/Samples/SQLServer/SQLServerProducer/App.config" "Source/Samples/SQLServer/SQLServerProducerOutbox/App.config" "Source/Samples/SQLServer/SQLServerConsumerInbox/App.config"; do
                                    sed -i "s|key=\\"Database\\" value=\\"[^\\"]*\\"|key=\\"Database\\" value=\\"${SQLSERVER_CONN}\\"|" "$cfg"
                                done
                            '''
                        }

                        sh '''
                            dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" \
                                -c Debug --no-build \
                                --filter "FullyQualifiedName~SqlServer" \
                                -f net10.0 \
                                --logger "junit;LogFilePath=$WORKSPACE/junit-results/sqlserver-{assembly}.{framework}.xml"
                        '''
                        stash includes: 'junit-results/**/*.xml', name: 'junit-sqlserver', allowEmpty: true
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
                                -f net10.0 \
                                --logger "junit;LogFilePath=$WORKSPACE/junit-results/redis-{assembly}.{framework}.xml"
                        '''
                        stash includes: 'junit-results/**/*.xml', name: 'junit-redis', allowEmpty: true
                    }
                }
            }
        }
    }

    post {
        always {
            // Pipeline-level post action — agent is `none`, so wrap in a node.
            // Unstash each per-stage junit bundle inside its own try/catch so an
            // early-stage failure that never produced a stash doesn't break the
            // publish for the rest. The junit step itself is tolerant of empty
            // results via allowEmptyResults.
            node('docker') {
                script {
                    def junitStashes = [
                        'junit-ci',
                        'junit-postgresql',
                        'junit-sqlserver',
                        'junit-redis'
                    ]
                    junitStashes.each { s ->
                        try { unstash s } catch (Exception e) { echo "JUnit unstash '${s}' skipped: ${e.message}" }
                    }
                }
                junit allowEmptyResults: true, testResults: 'junit-results/**/*.xml'
            }
        }
        failure {
            echo 'Pipeline failed. Check stage logs for details.'
        }
        success {
            echo 'Pipeline completed successfully.'
        }
    }
}
