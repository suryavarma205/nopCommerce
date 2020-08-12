pipeline {
  agent any
  stages{
    stage('Build') {
      steps{
        echo "Building project"
         sh './mvnw package'
      }
    }
     stage('Archive') {
      steps{
        echo "Archiving project"
        archiveArtifacts artifacts: '**/*.sln', followSymlinks: false
      }
    }
  }
}
